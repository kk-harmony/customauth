namespace CustomOAuthServer.Application.Admin;

public sealed record ClientResponse(
    string ClientId,
    string? DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes,
    bool RequirePkce);

public sealed record CreateClientRequest(
    string ClientId,
    string DisplayName,
    string ClientType,
    string? ClientSecret,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string>? GrantTypes,
    IReadOnlyList<string>? Scopes,
    bool RequirePkce = true);

public sealed record UpdateClientRequest(
    string? DisplayName,
    string? ClientSecret,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris,
    IReadOnlyList<string>? GrantTypes,
    IReadOnlyList<string>? Scopes,
    bool? RequirePkce);
