namespace CustomOAuthServer.Api.Configuration;

public static class ProductionConfigurationValidator
{
    private static readonly string[] RequiredEnvironmentVariables =
    [
        "ConnectionStrings__DefaultConnection",
        "OAuthServer__Issuer",
        "OAuthServer__SigningCertificatePath",
        "OAuthServer__SigningCertificatePassword",
        "OAuthServer__CorsRootDomain"
    ];

    public static void ValidateIfProduction(IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var missing = RequiredEnvironmentVariables
            .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Production requires the following environment variables (secrets are not read from appsettings): "
                + string.Join(", ", missing));
        }
    }
}
