using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightProxy.TplSocket;

public static partial class TplSocketExtensions
{
    public static async Task<Result> SendWithTimeoutAsync(
        this Socket socket,
        byte[]      buffer,
        int         offset,
        int         size,
        SocketFlags socketFlags,
        int         timeoutMs)
    {
        try
        {
            var asyncResult = socket.BeginSend(buffer, offset, size, socketFlags, null, null);

            if (asyncResult == null)
            {
                return Result.Fail<int>("");
            }

            var sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndSend(asyncResult));

            if (sendTask != await Task.WhenAny(sendTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
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