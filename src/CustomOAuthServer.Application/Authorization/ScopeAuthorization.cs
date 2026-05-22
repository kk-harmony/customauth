using System.Security.Claims;

namespace CustomOAuthServer.Application.Authorization;

public static class ScopeAuthorization
{
    public static IReadOnlyList<string> FilterScopesForUser(
        IEnumerable<string> requestedScopes,
        IReadOnlyList<string> userRoles)
    {
        var scopes = requestedScopes.ToList();
        if (!userRoles.Contains(UserRoles.Admin, StringComparer.Ordinal))
        {
            scopes.RemoveAll(s => string.Equals(s, AdminPolicies.AdminScope, StringComparison.Ordinal));
        }

        return scopes;
    }

    public static bool HasAdminAccess(ClaimsPrincipal user)
    {
        if (user.IsInRole(UserRoles.Admin))
        {
            return true;
        }

        foreach (var claim in user.FindAll("scope"))
        {
            foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(scope, AdminPolicies.AdminScope, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
