using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightProxy.TplSocket;

public static partial class TplSocketExtensions
{
    public static async Task<Result<int>> ReceiveWithTimeoutAsync(
        this Socket socket,
        byte[]      buffer,
        int         offset,
        int         size,
        SocketFlags socketFlags,
        int         timeoutMs)
    {
        int bytesReceived;
        try
        {
            var asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);

            if (asyncResult == null)
            {
                return Result.Fail<int>("");
            }

            var receiveTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));

            if (receiveTask == await Task.WhenAny(receiveTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
            {
                bytesReceived = await receiveTask.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException();
            }
        }
        catch (SocketException ex)
        {
            return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
        }
        catch (TimeoutException ex)
        {
            return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
        }

        return Result.Ok(bytesReceived);
    }
}