namespace CustomOAuthServer.Application.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int LoginRateLimitPerMinute { get; set; } = 20;

    public int MinPasswordLength { get; set; } = 12;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
}
