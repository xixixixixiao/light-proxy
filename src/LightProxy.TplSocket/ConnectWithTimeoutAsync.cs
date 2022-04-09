using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightProxy.TplSocket;

public static partial class TplSocketExtensions
{
    public static async Task<Result> ConnectWithTimeoutAsync(
        this Socket socket,
        string      remoteIpAddress,
        int         port,
        int         timeoutMs)
    {
        try
        {
            var connectTask = Task.Factory.FromAsync(
                socket.BeginConnect,
                socket.EndConnect,
                remoteIpAddress,
                port,
                null);

            if (connectTask == await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
            {
                await connectTask.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException();
            }
        }
        catch (SocketException ex)
        {
            return Result.Fail($"{ex.Message} ({ex.GetType()})");
        }
        catch (TimeoutException ex)
        {
            return Result.Fail($"{ex.Message} ({ex.GetType()})");
        }

        return Result.Ok();
    }

    public static async Task<Result> ConnectWithTimeoutAsync(
        this Socket socket,
        IPEndPoint  remoteIPEndPoint,
        int         timeoutMs)
    {
        try
        {
            var connectTask = Task.Factory.FromAsync(
                socket.BeginConnect,
                socket.EndConnect,
                remoteIPEndPoint,
                null);

            if (connectTask == await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
            {
                await connectTask.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException();
            }
        }
        catch (SocketException ex)
        {
            return Result.Fail($"{ex.Message} ({ex.GetType()})");
        }
        catch (TimeoutException ex)
        {
            return Result.Fail($"{ex.Message} ({ex.GetType()})");
        }

        return Result.Ok();
    }
}