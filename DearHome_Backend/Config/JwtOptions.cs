namespace DearHome_Backend.Config;

/// <summary>
/// JWT signing settings, bound from the "Jwt" configuration section (Jwt:Key, Jwt:Issuer, Jwt:Audience).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Key { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
}
