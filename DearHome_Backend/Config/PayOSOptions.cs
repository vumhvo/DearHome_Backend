using Microsoft.Extensions.Configuration;

namespace DearHome_Backend.Config;

/// <summary>
/// PayOS settings, bound from the "PayOS" configuration section
/// (PayOS:BaseUrl, PayOS:PAYOS_CLIENT_ID, PayOS:PAYOS_API_KEY, PayOS:PAYOS_CHECKSUM_KEY).
/// </summary>
public class PayOSOptions
{
    public const string SectionName = "PayOS";

    public string BaseUrl { get; set; } = string.Empty;

    [ConfigurationKeyName("PAYOS_CLIENT_ID")]
    public string ClientId { get; set; } = string.Empty;

    [ConfigurationKeyName("PAYOS_API_KEY")]
    public string ApiKey { get; set; } = string.Empty;

    [ConfigurationKeyName("PAYOS_CHECKSUM_KEY")]
    public string ChecksumKey { get; set; } = string.Empty;
}
