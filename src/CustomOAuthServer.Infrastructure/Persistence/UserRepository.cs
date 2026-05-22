using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.Models;
using Dapper;
using Npgsql;

namespace CustomOAuthServer.Infrastructure.Persistence;

public sealed class UserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    private const string SelectColumns =
        "id AS Id, username AS Username, email AS Email, display_name AS DisplayName, created_at AS CreatedAt";

    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await connection.QueryAsync<ApplicationUser>(
            new CommandDefinition(
                $"""
                 SELECT {SelectColumns}
                 FROM app_users
                 ORDER BY username
                 """,
                cancellationToken: cancellationToken));

        return users.Select(ToResponse).ToList();
    }

    public async Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(
            new CommandDefinition(
                $"""
                 SELECT {SelectColumns}
                 FROM app_users
                 WHERE id = @Id
                 """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<ApplicationUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(
            new CommandDefinition(
                $"""
                 SELECT {SelectColumns}
                 FROM app_users
                 WHERE username = @Username
                 """,
                new { Username = username },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var hash = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT password_hash FROM app_users WHERE username = @Username",
                new { Username = username },
                cancellationToken: cancellationToken));

        if (hash is null)
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await FindByUsernameAsync(request.Username, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Username '{request.Username}' is already taken.");
        }

        var id = $"user-{Guid.NewGuid():N}";
        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO app_users (id, username, email, display_name, password_hash)
                VALUES (@Id, @Username, @Email, @DisplayName, @PasswordHash)
                """,
                new
                {
                    Id = id,
                    request.Username,
                    request.Email,
                    request.DisplayName,
                    PasswordHash = hash
                },
                cancellationToken: cancellationToken));

        return (await FindByIdAsync(id, cancellationToken) is { } user
            ? ToResponse(user)
            : throw new InvalidOperationException("Failed to load created user."));
    }

    public async Task<UserResponse?> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await FindByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var email = request.Email ?? existing.Email;
        var displayName = request.DisplayName ?? existing.DisplayName;
        var passwordHash = request.Password is null
            ? null
            : BCrypt.Net.BCrypt.HashPassword(request.Password);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (passwordHash is null)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE app_users
                    SET email = @Email, display_name = @DisplayName
                    WHERE id = @Id
                    """,
                    new { Id = id, Email = email, DisplayName = displayName },
                    cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE app_users
                    SET email = @Email, display_name = @DisplayName, password_hash = @PasswordHash
                    WHERE id = @Id
                    """,
                    new { Id = id, Email = email, DisplayName = displayName, PasswordHash = passwordHash },
                    cancellationToken: cancellationToken));
        }

        var updated = await FindByIdAsync(id, cancellationToken);
        return updated is null ? null : ToResponse(updated);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM app_users WHERE id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));

        return rows > 0;
    }

    private static UserResponse ToResponse(ApplicationUser user) =>
        new(user.Id, user.Username, user.Email, user.DisplayName, user.CreatedAt);
}
