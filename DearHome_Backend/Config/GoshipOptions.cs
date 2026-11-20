using Microsoft.Extensions.Configuration;

namespace DearHome_Backend.Config;

/// <summary>
/// GoShip API settings, bound from the "Goship" configuration section
/// (Goship:BaseUrl and Goship:ApiSecret:username/password/client_id/client_secret).
/// </summary>
public class GoshipOptions
{
    public const string SectionName = "Goship";

    public string BaseUrl { get; set; } = string.Empty;
    public GoshipApiSecretOptions ApiSecret { get; set; } = new();
}

public class GoshipApiSecretOptions
{
    [ConfigurationKeyName("username")]
    public string Username { get; set; } = string.Empty;

    [ConfigurationKeyName("password")]
    public string Password { get; set; } = string.Empty;

    [ConfigurationKeyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [ConfigurationKeyName("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;
}
