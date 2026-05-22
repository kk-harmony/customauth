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
                    ScopeAuthorization.HasAdminAccess(context.User));
            });

        return services;
    }
}
