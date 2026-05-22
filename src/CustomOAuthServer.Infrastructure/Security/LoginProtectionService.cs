using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CustomOAuthServer.Infrastructure.Security;

public sealed class LoginProtectionService(
    NpgsqlDataSource dataSource,
    IOptions<SecurityOptions> securityOptions) : ILoginProtectionService
{
    public async Task<bool> IsLockedOutAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT lockout_until FROM login_attempts
            WHERE username = @username
            """;
        cmd.Parameters.AddWithValue("username", username);
        var lockoutUntil = await cmd.ExecuteScalarAsync(cancellationToken);
        if (lockoutUntil is DateTimeOffset until && until > DateTimeOffset.UtcNow)
        {
            return true;
        }

        return false;
    }

    public async Task RegisterFailedAttemptAsync(string username, CancellationToken cancellationToken = default)
    {
        var options = securityOptions.Value;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO login_attempts (username, failed_count, last_failed_at, lockout_until)
            VALUES (@username, 1, NOW(), NULL)
            ON CONFLICT (username) DO UPDATE SET
                failed_count = login_attempts.failed_count + 1,
                last_failed_at = NOW(),
                lockout_until = CASE
                    WHEN login_attempts.failed_count + 1 >= @maxAttempts
                    THEN NOW() + (@lockoutMinutes || ' minutes')::INTERVAL
                    ELSE login_attempts.lockout_until
                END
            """;
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("maxAttempts", options.MaxFailedLoginAttempts);
        cmd.Parameters.AddWithValue("lockoutMinutes", options.LockoutMinutes);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RegisterSuccessfulLoginAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM login_attempts WHERE username = @username";
        cmd.Parameters.AddWithValue("username", username);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
