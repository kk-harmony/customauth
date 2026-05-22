namespace CustomOAuthServer.Application.Security;

public static class SystemClients
{
    public static readonly HashSet<string> ProtectedClientIds = new(StringComparer.Ordinal)
    {
        "spa-client",
        "m2m-client",
        "obo-client",
        "introspection-client",
        "admin-client"
    };

    public static bool IsProtected(string clientId) => ProtectedClientIds.Contains(clientId);
}
