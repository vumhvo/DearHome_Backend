using System;

namespace DearHome_Backend.Config;

/// <summary>
/// NATS connection settings, bound from the "Nats" configuration section.
/// Credentials have no defaults: supply them via configuration (e.g. Nats__Username / Nats__Password env vars).
/// </summary>
public class NatsConfig
{
    public string Url { get; set; } = "nats://localhost:4222";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ConnectionTimeout { get; set; } = 2000;
}
