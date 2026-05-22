using CustomOAuthServer.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;

namespace CustomOAuthServer.Api.Authorization;

public static class AdminAuthorizationExtensions
{
    public static IServiceCollection AddAdminAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicies.PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var scopeClaims = context.User.FindAll("scope").Select(c => c.Value);
                    foreach (var claim in scopeClaims)
                    {
                        foreach (var scope in claim.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (string.Equals(scope, AdminPolicies.AdminScope, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                });
            });

        return services;
    }
}
