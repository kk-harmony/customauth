namespace CustomOAuthServer.Application.Abstractions;

public interface ILoginProtectionService
{
    Task<bool> IsLockedOutAsync(string username, CancellationToken cancellationToken = default);
    Task RegisterFailedAttemptAsync(string username, CancellationToken cancellationToken = default);
    Task RegisterSuccessfulLoginAsync(string username, CancellationToken cancellationToken = default);
}
