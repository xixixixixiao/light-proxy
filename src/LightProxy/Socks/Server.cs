using LightProxy.TplSocket;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LightProxy.Socks;

/// <summary>
/// SOCKS Protocol Version 5.
/// </summary>
public class Server
{
    private readonly Config _config;

    private Socket _listener;

    public Server(Config config)
    {
        _config = config;
    }

    public async Task<Result> StartAsync(CancellationToken token)
    {
        _listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(_config.EndPoint);
        _listener.Listen(10);

        while (token.IsCancellationRequested is false)
        {
            var accepted = await Task.Run(AcceptConnectionAsync, token);

            if (accepted.Failure)
            {
                continue;
            }

            var connection = new Connection(accepted.Value, _config);

            _ = Task.Run(() => connection.TransportAsync(token), token);
        }

        return Result.Ok();
    }

    private async Task<Result<Socket>> AcceptConnectionAsync()
    {
        return await TplSocketExtensions.AcceptAsync(_listener).ConfigureAwait(false);
    }
}