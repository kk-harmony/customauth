using System.Text.RegularExpressions;
using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Options;
using Microsoft.Extensions.Options;

namespace CustomOAuthServer.Infrastructure.Security;

public sealed partial class PasswordPolicyValidator(IOptions<SecurityOptions> securityOptions) : IPasswordPolicyValidator
{
    public IReadOnlyList<string> Validate(string? password)
    {
        var options = securityOptions.Value;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < options.MinPasswordLength)
        {
            errors.Add($"Password must be at least {options.MinPasswordLength} characters.");
        }

        if (options.RequireUppercase && !UppercaseRegex().IsMatch(password))
        {
            errors.Add("Password must contain an uppercase letter.");
        }

        if (options.RequireLowercase && !LowercaseRegex().IsMatch(password))
        {
            errors.Add("Password must contain a lowercase letter.");
        }

        if (options.RequireDigit && !DigitRegex().IsMatch(password))
        {
            errors.Add("Password must contain a digit.");
        }

        if (options.RequireNonAlphanumeric && !NonAlphanumericRegex().IsMatch(password))
        {
            errors.Add("Password must contain a non-alphanumeric character.");
        }

        return errors;
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex("[a-z]")]
    private static partial Regex LowercaseRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex NonAlphanumericRegex();
}
