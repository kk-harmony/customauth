namespace CustomOAuthServer.Application.Abstractions;

public interface IPasswordPolicyValidator
{
    IReadOnlyList<string> Validate(string? password);
}
