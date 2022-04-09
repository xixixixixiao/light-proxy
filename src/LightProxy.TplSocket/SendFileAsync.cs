using System.Net.Sockets;
using System.Threading.Tasks;

namespace LightProxy.TplSocket;

public static partial class TplSocketExtensions
{
    public static async Task<Result> SendFileAsync(this Socket socket, string filePath)
    {
        try
        {
            await Task.Factory.FromAsync(socket.BeginSendFile, socket.EndSendFile, filePath, null)
                      .ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            return Result.Fail($"{ex.Message} ({ex.GetType()})");
        }

        return Result.Ok();
    }
}