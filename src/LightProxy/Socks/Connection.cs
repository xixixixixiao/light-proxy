using LightProxy.TplSocket;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LightProxy.Socks;

public class Connection
{
    private const int BufferSize = 8192;

    private readonly Config _config;
    private readonly Socket _transfer;

    private Protocol _protocol;
    private Socket   _client;

    public Connection(Socket transfer, Config config)
    {
        _transfer = transfer;
        _config   = config;
    }

    public async Task TransportAsync(CancellationToken token)
    {
        // +========+              +--------+              +----------+              +==========+
        // [ Remote ] <<= data =>> | Client | <<= data =>> | Transfer | <<= data =>> [ Consumer ]
        // +========+              +--------+              +----------+              +==========+

        _protocol = new Protocol(_transfer, _config);

        var handshakeResult = await _protocol.HandshakeAsync();

        if (handshakeResult.Failure)
        {
            Close();
            return;
        }

        _client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var connectResult = await _client.ConnectWithTimeoutAsync(handshakeResult.Value, 30000)
                                         .ConfigureAwait(false);

        if (connectResult.Failure)
        {
            Close();
            return;
        }

        var upstream   = new NetworkStream(_client, true);
        var downstream = new NetworkStream(_transfer, true);

        {
            using var tunnel = new TcpTwoWayTunnel();
            await tunnel.Run(upstream, downstream);
        }

        Close();
    }

    public void Close()
    {
        try
        {
            _transfer?.Shutdown(SocketShutdown.Both);
            _transfer?.Close();
            _transfer?.Dispose();
        }
        catch (Exception)
        {
            //
        }

        try
        {
            _client?.Shutdown(SocketShutdown.Both);
            _client?.Close();
            _client?.Dispose();
        }
        catch (Exception)
        {
            //
        }
    }
}