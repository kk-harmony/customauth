namespace CustomOAuthServer.Application.Admin;

public sealed record UserResponse(
    string Id,
    string Username,
    string Email,
    string? DisplayName,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string? DisplayName);

public sealed record UpdateUserRequest(
    string? Email,
    string? DisplayName,
    string? Password);
