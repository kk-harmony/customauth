using CustomOAuthServer.Application.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace CustomOAuthServer.Api.Cors;

public static class CorsPolicyConfigurator
{
    public const string DefaultPolicyName = "default";

    public static void ConfigureDefaultPolicy(
        CorsPolicyBuilder policy,
        OAuthServerOptions options,
        IHostEnvironment environment)
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

        if (environment.IsDevelopment())
        {
            // Wildcard-style CORS for local dev (compatible with AllowCredentials).
            policy.SetIsOriginAllowed(_ => true);
            return;
        }

        policy.SetIsOriginAllowed(origin => IsOriginAllowed(origin, options));
    }

    internal static bool IsOriginAllowed(string origin, OAuthServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        foreach (var allowed in options.AllowedOrigins)
        {
            if (string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(options.CorsRootDomain))
        {
            return false;
        }

        var root = options.CorsRootDomain.Trim().TrimStart('.');
        var host = uri.Host;

        return host.Equals(root, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + root, StringComparison.OrdinalIgnoreCase);
    }
}
