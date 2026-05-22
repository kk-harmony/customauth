using System.Security.Cryptography.X509Certificates;
using CustomOAuthServer.Application.OAuth;
using CustomOAuthServer.Application.Options;
using CustomOAuthServer.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Infrastructure.OpenIddict;

public static class OpenIddictServerExtensions
{
    public static IServiceCollection AddCustomOpenIddictServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var oauthOptions = configuration.GetSection(OAuthServerOptions.SectionName).Get<OAuthServerOptions>()
            ?? new OAuthServerOptions();

        var issuerUri = new Uri(oauthOptions.Issuer, UriKind.Absolute);
        var allowHttp = string.Equals(issuerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (environment.IsProduction() && string.IsNullOrWhiteSpace(oauthOptions.SigningCertificatePath))
        {
            throw new InvalidOperationException(
                "OAuthServer__SigningCertificatePath is required in Production. " +
                "Set it via environment variables (see PRODUCTION.md).");
        }

        var useProductionCertificates = environment.IsProduction()
            && !string.IsNullOrWhiteSpace(oauthOptions.SigningCertificatePath);

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(oauthOptions.Issuer, UriKind.Absolute));

                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetUserInfoEndpointUris("/connect/userinfo")
                    .SetEndSessionEndpointUris("/connect/logout")
                    .SetIntrospectionEndpointUris("/connect/introspect")
                    .SetRevocationEndpointUris("/connect/revoke");

                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow()
                    .AllowCustomFlow(OAuthGrantTypes.TokenExchange);

                options.RequireProofKeyForCodeExchange();

                options.UseReferenceAccessTokens()
                    .UseReferenceRefreshTokens();

                options.RegisterScopes(
                    Scopes.OpenId,
                    Scopes.Email,
                    Scopes.Profile,
                    Scopes.Roles,
                    Scopes.OfflineAccess,
                    "api",
                    "admin");

                if (useProductionCertificates)
                {
                    var signing = LoadCertificate(
                        oauthOptions.SigningCertificatePath!,
                        oauthOptions.SigningCertificatePassword);
                    var encryptionPath = oauthOptions.EncryptionCertificatePath ?? oauthOptions.SigningCertificatePath!;
                    var encryptionPassword = oauthOptions.EncryptionCertificatePath is null
                        ? oauthOptions.SigningCertificatePassword
                        : oauthOptions.EncryptionCertificatePassword;
                    var encryption = string.Equals(encryptionPath, oauthOptions.SigningCertificatePath, StringComparison.OrdinalIgnoreCase)
                        ? signing
                        : LoadCertificate(encryptionPath, encryptionPassword);

                    options.AddSigningCertificate(signing)
                        .AddEncryptionCertificate(encryption);
                }
                else
                {
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }

                options.UseAspNetCore(aspNetCore =>
                {
                    aspNetCore.EnableAuthorizationEndpointPassthrough()
                        .EnableTokenEndpointPassthrough()
                        .EnableUserInfoEndpointPassthrough()
                        .EnableEndSessionEndpointPassthrough()
                        .EnableStatusCodePagesIntegration();

                    if (allowHttp || environment.IsDevelopment())
                    {
                        aspNetCore.DisableTransportSecurityRequirement();
                    }
                });
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    private static X509Certificate2 LoadCertificate(string path, string? password)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Certificate not found: {path}", path);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(path, password);
    }
}
