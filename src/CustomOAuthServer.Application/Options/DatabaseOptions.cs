namespace CustomOAuthServer.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Production must use EF migrations. Development may fall back to EnsureCreated when true.</summary>
    public bool AllowEnsureCreatedFallback { get; set; }

    /// <summary>When true, startup fails if OpenIddict tables are missing after migrate.</summary>
    public bool RequireOpenIddictSchema { get; set; } = true;
}
