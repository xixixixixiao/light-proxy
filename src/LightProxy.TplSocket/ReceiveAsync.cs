using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightProxy.TplSocket;

public static partial class TplSocketExtensions
{
    public static async Task<Result<int>> ReceiveAsync(
        this Socket socket,
        byte[]      buffer,
        int         offset,
        int         size,
        SocketFlags socketFlags)
    {
        int bytesReceived;
        try
        {
            var asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);

            if (asyncResult == null)
            {
                return  Result.Fail<int>("");
            }

            bytesReceived = await Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
        }
        catch (SocketException ex)
        {
            return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
        }

        return Result.Ok(bytesReceived);
    }
}