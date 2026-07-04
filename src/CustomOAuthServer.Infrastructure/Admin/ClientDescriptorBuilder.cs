using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.OAuth;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Infrastructure.Admin;

internal static class ClientDescriptorBuilder
{
    public static OpenIddictApplicationDescriptor BuildDescriptor(CreateClientRequest request)
    {
        ValidateClientType(request.ClientType);
        ValidateGrantTypes(request.GrantTypes);
        ValidateConfidentialSecret(request.ClientType, request.ClientSecret, required: true);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ClientType = ToClientType(request.ClientType),
            ClientSecret = request.ClientSecret,
            ConsentType = ConsentTypes.Implicit
        };

        ApplyUris(descriptor, request.RedirectUris, request.PostLogoutRedirectUris);
        ApplyPermissions(descriptor, request.GrantTypes, request.Scopes, request.RequirePkce);
        ClientAudienceProperties.SetAllowedAudiences(descriptor.Properties, request.AllowedAudiences);

        return descriptor;
    }

    public static void ApplyUpdate(OpenIddictApplicationDescriptor descriptor, UpdateClientRequest request)
    {
        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.ClientSecret is not null)
        {
            descriptor.ClientSecret = request.ClientSecret;
        }

        if (request.RedirectUris is not null || request.PostLogoutRedirectUris is not null)
        {
            descriptor.RedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Clear();
            ApplyUris(descriptor, request.RedirectUris, request.PostLogoutRedirectUris);
        }

        if (request.GrantTypes is not null || request.Scopes is not null || request.RequirePkce is not null)
        {
            var grantTypes = request.GrantTypes ?? ExtractGrantTypes(descriptor.Permissions);
            var scopes = request.Scopes ?? ExtractScopes(descriptor.Permissions);
            var requirePkce = request.RequirePkce ?? descriptor.Requirements.Contains(Requirements.Features.ProofKeyForCodeExchange);

            descriptor.Permissions.Clear();
            descriptor.Requirements.Clear();
            ApplyPermissions(descriptor, grantTypes, scopes, requirePkce);
        }

        if (request.AllowedAudiences is not null)
        {
            ClientAudienceProperties.SetAllowedAudiences(descriptor.Properties, request.AllowedAudiences);
        }
    }

    public static ClientResponse ToResponse(
        OpenIddictApplicationDescriptor descriptor,
        string clientId) =>
        new(
            clientId,
            descriptor.DisplayName,
            FromClientType(descriptor.ClientType),
            descriptor.RedirectUris.Select(u => u.ToString()).ToList(),
            descriptor.PostLogoutRedirectUris.Select(u => u.ToString()).ToList(),
            ExtractGrantTypes(descriptor.Permissions),
            ExtractScopes(descriptor.Permissions),
            ClientAudienceProperties.GetAllowedAudiences(descriptor.Properties),
            descriptor.Requirements.Contains(Requirements.Features.ProofKeyForCodeExchange));

    private static void ApplyUris(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string>? redirectUris,
        IReadOnlyList<string>? postLogoutRedirectUris)
    {
        if (redirectUris is not null)
        {
            foreach (var uri in redirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
            }
        }

        if (postLogoutRedirectUris is not null && postLogoutRedirectUris.Count > 0)
        {
            foreach (var uri in postLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri, UriKind.Absolute));
            }
        }
        else if (redirectUris is not null)
        {
            foreach (var uri in redirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri, UriKind.Absolute));
            }
        }
    }

    private static void ApplyPermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string>? grantTypes,
        IReadOnlyList<string>? scopes,
        bool requirePkce)
    {
        var grants = grantTypes?.Count > 0
            ? grantTypes
            : ["client_credentials"];

        descriptor.Permissions.Add(Permissions.Endpoints.Token);

        foreach (var grant in grants)
        {
            switch (grant)
            {
                case "authorization_code":
                    descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
                    descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
                    descriptor.Permissions.Add(Permissions.Prefixes.Endpoint + "userinfo");
                    if (requirePkce)
                    {
                        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                    }
                    break;
                case "refresh_token":
                    descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;
                case "client_credentials":
                    descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
                    break;
                case "token_exchange":
                    descriptor.Permissions.Add(Permissions.Prefixes.GrantType + OAuthGrantTypes.TokenExchange);
                    break;
                default:
                    throw new ArgumentException($"Unsupported grant type: {grant}", nameof(grantTypes));
            }
        }

        foreach (var scope in scopes ?? [])
        {
            switch (scope)
            {
                case Scopes.OpenId:
                    descriptor.Permissions.Add(Permissions.Prefixes.Scope + Scopes.OpenId);
                    break;
                case Scopes.Email:
                    descriptor.Permissions.Add(Permissions.Scopes.Email);
                    break;
                case Scopes.Profile:
                    descriptor.Permissions.Add(Permissions.Scopes.Profile);
                    break;
                case Scopes.Roles:
                    descriptor.Permissions.Add(Permissions.Scopes.Roles);
                    break;
                case Scopes.OfflineAccess:
                    descriptor.Permissions.Add(Permissions.Prefixes.Scope + Scopes.OfflineAccess);
                    break;
                default:
                    descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
                    break;
            }
        }

        if (grants.Any(g => string.Equals(g, "client_credentials", StringComparison.Ordinal)))
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Introspection);
            descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
        }
    }

    private static IReadOnlyList<string> ExtractGrantTypes(IEnumerable<string> permissions)
    {
        var grants = new List<string>();
        if (permissions.Contains(Permissions.GrantTypes.AuthorizationCode))
        {
            grants.Add("authorization_code");
        }

        if (permissions.Contains(Permissions.GrantTypes.RefreshToken))
        {
            grants.Add("refresh_token");
        }

        if (permissions.Contains(Permissions.GrantTypes.ClientCredentials))
        {
            grants.Add("client_credentials");
        }

        if (permissions.Any(p => p == Permissions.Prefixes.GrantType + OAuthGrantTypes.TokenExchange))
        {
            grants.Add("token_exchange");
        }

        return grants;
    }

    private static IReadOnlyList<string> ExtractScopes(IEnumerable<string> permissions)
    {
        var scopes = new List<string>();
        foreach (var permission in permissions)
        {
            if (permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            {
                scopes.Add(permission[Permissions.Prefixes.Scope.Length..]);
            }
        }

        return scopes;
    }

    private static string ToClientType(string clientType) =>
        clientType switch
        {
            "public" => ClientTypes.Public,
            "confidential" => ClientTypes.Confidential,
            _ => throw new ArgumentException("ClientType must be 'public' or 'confidential'.", nameof(clientType))
        };

    private static string FromClientType(string? clientType) =>
        clientType == ClientTypes.Public ? "public" : "confidential";

    private static void ValidateClientType(string clientType)
    {
        if (clientType is not "public" and not "confidential")
        {
            throw new ArgumentException("ClientType must be 'public' or 'confidential'.", nameof(clientType));
        }
    }

    private static void ValidateGrantTypes(IReadOnlyList<string>? grantTypes)
    {
        if (grantTypes is null)
        {
            return;
        }

        foreach (var grant in grantTypes)
        {
            if (grant is not ("authorization_code" or "refresh_token" or "client_credentials" or "token_exchange"))
            {
                throw new ArgumentException($"Unsupported grant type: {grant}", nameof(grantTypes));
            }
        }
    }

    private static void ValidateConfidentialSecret(string clientType, string? secret, bool required)
    {
        if (clientType == "confidential" && required && string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("ClientSecret is required for confidential clients.");
        }
    }
}
