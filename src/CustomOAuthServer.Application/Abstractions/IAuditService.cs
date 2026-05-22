namespace CustomOAuthServer.Application.Abstractions;

public interface IAuditService
{
    Task WriteAsync(
        string eventType,
        string? actorSubject,
        string? target,
        object? details = null,
        CancellationToken cancellationToken = default);
}
