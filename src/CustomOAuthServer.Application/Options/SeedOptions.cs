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

    /// <summary>When true, ensures a default admin user exists (username <c>admin</c> by default).</summary>
    public bool DefaultAdminUser { get; set; } = true;

    /// <summary>Default admin username. Set via <c>Seed__DefaultAdminUsername</c>.</summary>
    public string DefaultAdminUsername { get; set; } = "admin";

    /// <summary>Default admin email. Set via <c>Seed__DefaultAdminEmail</c>.</summary>
    public string DefaultAdminEmail { get; set; } = "admin@localhost";

    /// <summary>
    /// Password for the default admin user. Set via <c>Seed__DefaultAdminPassword</c>.
    /// When unset, <c>ThisIsP@ss</c> is used.
    /// </summary>
    public string? DefaultAdminPassword { get; set; }

    /// <summary>OAuth client secrets keyed by client_id. Values must come from configuration/secrets store.</summary>
    public Dictionary<string, string> ClientSecrets { get; set; } = new(StringComparer.Ordinal);
}
