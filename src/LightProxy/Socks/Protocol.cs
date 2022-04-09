using LightProxy.TplSocket;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LightProxy.Socks;

public class Protocol
{
    private readonly Socket _transfer;
    private readonly Config _config;

    public Protocol(Socket transfer, Config config)
    {
        _transfer = transfer;
        _config   = config;
    }

    public async Task<Result<IPEndPoint>> HandshakeAsync()
    {
        var methodVerified = await VerifyMethodAsync().ConfigureAwait(false);

        if (methodVerified.Failure)
        {
            return Result.Fail<IPEndPoint>(methodVerified.Error);
        }

        var authorized = await AuthorizeAsync().ConfigureAwait(false);

        if (authorized.Failure)
        {
            return Result.Fail<IPEndPoint>(authorized.Error);
        }

        return await AnalyseAsync().ConfigureAwait(false);
    }

    private async Task<Result> VerifyMethodAsync()
    {
        var buffer = new byte[0x200];
        var received = await _transfer.ReceiveWithTimeoutAsync(
            buffer,
            0,
            0x200,
            SocketFlags.None,
            3000).ConfigureAwait(false);

        if (received.Failure || received.Value < 0x02)
        {
            return Result.Fail("Failed to hand shake.");
        }

        if (received.Value == 0x00)
        {
            return Result.Fail("No data received.");
        }

        // RECEIVED:
        //    +----+----------+----------+
        //    |VER | NMETHODS | METHODS  |
        //    +----+----------+----------+
        //    | 1  |    1     | 1 to 255 |
        //    +----+----------+----------+

        var version = buffer[0x00];
        var method  = buffer[0x01];

        if (version is not 0x05)
        {
            return Result.Fail($"The socks version {version} is not supported.");
        }

        // REPLY:
        //    +----+--------+
        //    |VER | METHOD |
        //    +----+--------+
        //    | 1  |   1    |
        //    +----+--------+
        // METHOD:
        //    X'00' NO AUTHENTICATION REQUIRED
        //    X'01' GSSAPI
        //    X'02' USERNAME/PASSWORD
        //    X'03' to X'7F' IANA ASSIGNED
        //    X'80' to X'FE' RESERVED FOR PRIVATE METHODS
        //    X'FF' NO ACCEPTABLE METHODS

        byte reply;

        switch (method)
        {
            case 0xFF:
                reply = 0xFF;
                break;
            case 0x80:
                reply = 0x80;
                break;
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x03:
                reply = _config.AuthType switch
                {
                    AuthType.None     => 0x00,
                    AuthType.Password => 0x02,
                    _                 => 0xFF,
                };

                break;
            default:
                reply = 0xFF;
                break;
        }

        var replied = await _transfer.SendWithTimeoutAsync(
            new byte[] { 0x05, reply },
            0x00,
            0x02,
            SocketFlags.None,
            3000).ConfigureAwait(false);

        if (replied.Failure)
        {
            return Result.Fail($"Failed to hand shake: {replied.Error}");
        }

        return reply == 0xFF ? Result.Fail("NO ACCEPTABLE METHODS") : Result.Ok();
    }

    private async Task<Result> AuthorizeAsync()
    {
        switch (_config.AuthType)
        {
            case AuthType.None:
                return Result.Ok();
            case AuthType.Password:
                // +----+------+----------+------+----------+
                // |VER | ULEN |  UNAME   | PLEN |  PASSWD  |
                // +----+------+----------+------+----------+
                // | 1  |  1   | 1 to 255 |  1   | 1 to 255 |
                // +----+------+----------+------+----------+

                var buffer = new byte[0x400];
                var received = await _transfer.ReceiveWithTimeoutAsync(
                    buffer,
                    0x00,
                    0x400,
                    SocketFlags.None,
                    3000).ConfigureAwait(false);

                if (received.Failure || received.Value < 0x01 + 0x01 + 0x01)
                {
                    return Result.Fail("Authentication failed.");
                }

                var usernameLength = buffer[0x01];
                var passwordLength = buffer[0x01 + usernameLength + 0x01];

                var username = Encoding.UTF8.GetString(buffer, 0x01 + 0x01, usernameLength);
                var password = Encoding.UTF8.GetString(buffer, 0x01 + 0x01 + usernameLength + 0x01, passwordLength);

                // +----+--------+
                // |VER | STATUS |
                // +----+--------+
                // | 1  |   1    |
                // +----+--------+

                var verified = _config.Account.Verify(username, password);
                var reply    = (byte)(verified ? 0x00 : 0xFF);

                var replied = await _transfer.SendWithTimeoutAsync(
                    new byte[] { 0x05, reply },
                    0x00,
                    0x02,
                    SocketFlags.None,
                    3000).ConfigureAwait(false);

                if (replied.Failure)
                {
                    return Result.Fail($"Authentication failed: {replied.Error}");
                }

                return verified ? Result.Ok() : Result.Fail("Incorrect username or password");

            default:
                return Result.Fail("Authentication failed.");
        }
    }

    private async Task<Result<IPEndPoint>> AnalyseAsync()
    {
        // +----+-----+-------+------+----------+----------+
        // |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
        // +----+-----+-------+------+----------+----------+
        // | 1  |  1  | X'00' |  1   | Variable |    2     |
        // +----+-----+-------+------+----------+----------+

        var headerBuffer = new byte[0x04];
        var portBuffer   = new byte[0x02];

        await _transfer.ReceiveWithTimeoutAsync(headerBuffer, 0x00, 0x04, SocketFlags.None, 3000).ConfigureAwait(false);

        var version = headerBuffer[0x00];
        var command = headerBuffer[0x01];
        var type    = headerBuffer[0x03];

        if (version != 0x05)
        {
            return Result.Fail<IPEndPoint>($"The protocol version: {version} is not supported.");
        }

        switch (command)
        {
            case 0x01: // CONNECT.
                break;
            case 0x02: // BIND.
                break;
            case 0x03: // UDP ASSOCIATE.
                break;
            default: // Exception.
                return Result.Fail<IPEndPoint>($"The command: {command} is not supported.");
        }

        // +----+-----+-------+------+----------+----------+
        // |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
        // +----+-----+-------+------+----------+----------+
        // | 1  |  1  | X'00' |  1   | Variable |    2     |
        // +----+-----+-------+------+----------+----------+
        //
        // REP    Reply field:
        //    X'00' succeeded
        //    X'01' general SOCKS server failure
        //    X'02' connection not allowed by ruleset
        //    X'03' Network unreachable
        //    X'04' Host unreachable
        //    X'05' Connection refused
        //    X'06' TTL expired
        //    X'07' Command not supported
        //    X'08' Address type not supported
        //    X'09' to X'FF' unassigned

        byte reply;

        var address = IPAddress.None;
        var port    = ushort.MinValue;

        switch (type)
        {
            case 0x01:
                // IP V4 address:
                //
                // the address is a version-4 IP address, with a length of 4 octets.

                var ipv4Buffer = new byte[0x04];

                _ = await _transfer.ReceiveAsync(ipv4Buffer, 0x00, 0x04, SocketFlags.None).ConfigureAwait(false);
                _ = await _transfer.ReceiveAsync(portBuffer, 0x00, 0x02, SocketFlags.None).ConfigureAwait(false);

                Array.Reverse(portBuffer);

                address = new IPAddress(ipv4Buffer);
                port    = BitConverter.ToUInt16(portBuffer, 0x00);
                reply   = 0x00;

                break;
            case 0x03:
                // DOMAINNAME:
                //
                // the address field contains a fully-qualified domain name.  The first
                // octet of the address field contains the number of octets of name that
                // follow, there is no terminating NUL octet.

                var lengthBuffer = new byte[0x01];

                _ = await _transfer.ReceiveAsync(lengthBuffer, 0x00, 0x01, SocketFlags.None).ConfigureAwait(false);

                var domainLength = lengthBuffer[0x00];
                var domainBuffer = new byte[domainLength];

                _ = await _transfer.ReceiveAsync(domainBuffer, 0x00, domainLength, SocketFlags.None)
                                   .ConfigureAwait(false);

                var domain = Encoding.UTF8.GetString(domainBuffer);

                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(domain).ConfigureAwait(false);

                    if (addresses.Any())
                    {
                        _ = await _transfer.ReceiveAsync(portBuffer, 0x00, 0x02, SocketFlags.None)
                                           .ConfigureAwait(false);

                        Array.Reverse(portBuffer);

                        address = addresses[0x00];
                        port    = BitConverter.ToUInt16(portBuffer, 0x00);
                        reply   = 0x00;
                    }
                    else
                    {
                        reply = 0x04; // Host unreachable.
                    }
                }
                catch (Exception)
                {
                    reply = 0x04;
                }

                break;
            case 0x04:
                // IP V6 address:
                //
                // the address is a version-6 IP address, with a length of 16 octets.

                var ipv6Buffer = new byte[0x10];

                _ = await _transfer.ReceiveAsync(ipv6Buffer, 0x00, 0x10, SocketFlags.None).ConfigureAwait(false);
                _ = await _transfer.ReceiveAsync(portBuffer, 0x00, 0x02, SocketFlags.None).ConfigureAwait(false);

                Array.Reverse(portBuffer);

                address = new IPAddress(ipv6Buffer);
                port    = BitConverter.ToUInt16(portBuffer, 0x00);
                reply   = 0x00;

                break;
            default:
                address = IPAddress.None;
                port    = ushort.MinValue;
                reply   = 0x08; // Address type not supported.
                break;
        }

        var boundAddress = _config.EndPoint.Address.GetAddressBytes();
        var boundPort    = GetPortBytes(_config.EndPoint.Port);

        var memoryStream = new MemoryStream();

        await memoryStream.WriteAsync(new byte[] { 0x05 }, 0x00, 0x01).ConfigureAwait(false);         // VER
        await memoryStream.WriteAsync(new[] { reply }, 0x00, 0x01).ConfigureAwait(false);             // REP
        await memoryStream.WriteAsync(new byte[] { 0x00 }, 0x00, 0x01).ConfigureAwait(false);         // RSV
        await memoryStream.WriteAsync(new byte[] { 0x01 }, 0x00, 0x01).ConfigureAwait(false);         // ATYP
        await memoryStream.WriteAsync(boundAddress, 0x00, boundAddress.Length).ConfigureAwait(false); // BND.ADDR
        await memoryStream.WriteAsync(boundPort, 0x00, boundPort.Length).ConfigureAwait(false);       // BND.PORT

        var buffer = memoryStream.ToArray();

        _ = await _transfer.SendWithTimeoutAsync(
            buffer,
            0,
            buffer.Length,
            SocketFlags.None,
            3000).ConfigureAwait(false);

        return Result.Ok(new IPEndPoint(address, port));
    }

    private static byte[] GetPortBytes(int port)
    {
        return new[]
        {
            Convert.ToByte(port / 256),
            Convert.ToByte(port % 256),
        };
    }
}