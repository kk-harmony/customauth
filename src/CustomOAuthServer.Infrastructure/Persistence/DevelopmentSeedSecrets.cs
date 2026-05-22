namespace CustomOAuthServer.Infrastructure.Persistence;

/// <summary>Local development defaults only. Never used in Production.</summary>
internal static class DevelopmentSeedSecrets
{
    public const string DemoUserPassword = "Password123!";

    public static readonly IReadOnlyDictionary<string, string> ClientSecrets =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["m2m-client"] = "m2m-secret-change-in-production",
            ["obo-client"] = "obo-secret-change-in-production",
            ["introspection-client"] = "introspection-secret-change-in-production",
            ["admin-client"] = "admin-secret-change-in-production"
        };
}
