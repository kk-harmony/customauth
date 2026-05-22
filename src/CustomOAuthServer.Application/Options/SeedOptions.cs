namespace CustomOAuthServer.Application.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When false, no OAuth clients, scopes, or users are seeded at startup.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, seeds alice/bob demo users (never in Production by default).</summary>
    public bool DemoUsers { get; set; } = true;

    /// <summary>Demo user password (required when DemoUsers is true). Set via environment variable in deploy.</summary>
    public string? DemoUserPassword { get; set; }

    /// <summary>OAuth client secrets keyed by client_id. Values must come from configuration/secrets store.</summary>
    public Dictionary<string, string> ClientSecrets { get; set; } = new(StringComparer.Ordinal);
}
