using System.Security.Claims;
using CustomOAuthServer.Application.Authorization;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Application.OAuth;

public enum TokenExchangeFailureKind
{
    InvalidRequest,
    InvalidGrant,
    InvalidTarget
}

public sealed record TokenExchangeFailure(TokenExchangeFailureKind Kind, string Description);

public static class TokenExchangeValidator
{
    public static TokenExchangeFailure? ValidateSubjectTokenType(string? subjectTokenType)
    {
        if (string.IsNullOrEmpty(subjectTokenType))
        {
            return null;
        }

        if (!string.Equals(subjectTokenType, OAuthTokenTypes.AccessToken, StringComparison.Ordinal))
        {
            return new TokenExchangeFailure(
                TokenExchangeFailureKind.InvalidRequest,
                "The subject_token_type must be an access token.");
        }

        return null;
    }

    public static TokenExchangeFailure? ValidateAudience(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience))
        {
            return new TokenExchangeFailure(
                TokenExchangeFailureKind.InvalidRequest,
                "The audience parameter is required.");
        }

        return null;
    }

    public static TokenExchangeFailure? ValidateAudienceAllowlist(
        string audience,
        IReadOnlyList<string> allowedAudiences)
    {
        if (allowedAudiences.Count == 0)
        {
            return new TokenExchangeFailure(
                TokenExchangeFailureKind.InvalidTarget,
                "The client is not configured with any allowed audiences.");
        }

        if (!allowedAudiences.Contains(audience, StringComparer.Ordinal))
        {
            return new TokenExchangeFailure(
                TokenExchangeFailureKind.InvalidTarget,
                "The requested audience is not allowed for this client.");
        }

        return null;
    }

    public static IReadOnlyList<string> ResolveExchangeScopes(
        IEnumerable<string> requestedScopes,
        ClaimsPrincipal subjectPrincipal,
        IReadOnlyList<string> userRoles)
    {
        var subjectScopes = GetPrincipalScopes(subjectPrincipal);
        var scopes = requestedScopes.ToList();

        if (scopes.Count == 0)
        {
            scopes = subjectScopes.ToList();
        }
        else
        {
            scopes = scopes
                .Where(scope => subjectScopes.Contains(scope, StringComparer.Ordinal))
                .ToList();
        }

        return ScopeAuthorization.FilterScopesForUser(scopes, userRoles);
    }

    private static IReadOnlyList<string> GetPrincipalScopes(ClaimsPrincipal principal) =>
        principal.FindAll(Claims.Scope)
            .Select(claim => claim.Value)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToList();
}
