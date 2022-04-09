using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LightProxy.Socks;

public class TcpTwoWayTunnel : IDisposable
{
    private const int BufferSize = 8192;

    private CancellationTokenSource _cancellationTokenSource;

    public TcpTwoWayTunnel()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task Run(NetworkStream client, NetworkStream host)
    {
        await Task.WhenAny(
            Tunnel(client, host, _cancellationTokenSource.Token),
            Tunnel(host, client, _cancellationTokenSource.Token));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }

    private static async Task Tunnel(Stream source, Stream destination, CancellationToken token)
    {
        var buffer = new byte[BufferSize];

        try
        {
            int size;
            do
            {
                size = await source.ReadAsync(buffer, 0, BufferSize, token);
                await destination.WriteAsync(buffer, 0, size, token);
            } while (size > 0 && !token.IsCancellationRequested);
        }
        catch
        {
            //
        }
    }
}