using LightProxy.Socks;
using System.Net;

Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine("");
Console.WriteLine("    _/        _/_/_/    _/_/_/  _/    _/  _/_/_/_/_/    ");
Console.WriteLine("   _/          _/    _/        _/    _/      _/         ");
Console.WriteLine("  _/          _/    _/  _/_/  _/_/_/_/      _/          ");
Console.WriteLine(" _/          _/    _/    _/  _/    _/      _/           ");
Console.WriteLine("_/_/_/_/  _/_/_/    _/_/_/  _/    _/      _/            ");
Console.WriteLine("");
Console.WriteLine("    _/_/_/    _/_/_/      _/_/    _/      _/  _/      _/");
Console.WriteLine("   _/    _/  _/    _/  _/    _/    _/  _/      _/  _/   ");
Console.WriteLine("  _/_/_/    _/_/_/    _/    _/      _/          _/      ");
Console.WriteLine(" _/        _/    _/  _/    _/    _/  _/        _/       ");
Console.WriteLine("_/        _/    _/    _/_/    _/      _/      _/        ");
Console.WriteLine("");
Console.ResetColor();

var config = new Config
{
    EndPoint = new IPEndPoint(IPAddress.Any, 1880)
};
var server = new Server(config);

var source = new CancellationTokenSource();
var token  = source.Token;

await server.StartAsync(token);

Console.ReadLine();