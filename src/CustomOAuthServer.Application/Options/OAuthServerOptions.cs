namespace CustomOAuthServer.Application.Options;

public sealed class OAuthServerOptions
{
    public const string SectionName = "OAuthServer";

    public string Issuer { get; set; } = "https://localhost:5001/";
    public string[] AllowedOrigins { get; set; } = ["https://localhost:3000"];

    /// <summary>
    /// Production: allows https://{root}, https://*.{root}, and any host equal to root (e.g. example.com).
    /// Development ignores this and allows any origin.
    /// </summary>
    public string? CorsRootDomain { get; set; }

    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>Path to a PFX used for signing tokens in Production (optional; dev certs used when empty).</summary>
    public string? SigningCertificatePath { get; set; }

    public string? SigningCertificatePassword { get; set; }

    /// <summary>Path to a PFX used for encrypting tokens in Production (optional; defaults to signing cert).</summary>
    public string? EncryptionCertificatePath { get; set; }

    public string? EncryptionCertificatePassword { get; set; }
}
