namespace CustomOAuthServer.Application.Models;

public sealed class ApplicationUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string[] Roles { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
}
