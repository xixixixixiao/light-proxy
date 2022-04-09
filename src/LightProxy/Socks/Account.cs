using System;

namespace LightProxy.Socks;

public enum AuthType
{
    None     = 0x00,
    Password = 0x01,
}

public class Account
{
    public string Username { get; set; }
    public string Password { get; set; }

    public bool Verify(string username, string password)
    {
        return string.Equals(Username, username, StringComparison.CurrentCultureIgnoreCase) &&
               string.Equals(Password, password, StringComparison.CurrentCultureIgnoreCase);
    }
}