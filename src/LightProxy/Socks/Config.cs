using System.Net;

namespace LightProxy.Socks;

public class Config
{
    public AuthType   AuthType { get; set; } = AuthType.None;
    public Account    Account  { get; set; }
    public IPEndPoint EndPoint { get; set; } = new(IPAddress.Loopback, 1080);
}