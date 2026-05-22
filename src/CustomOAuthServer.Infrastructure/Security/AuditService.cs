using System.Text.Json;
using CustomOAuthServer.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CustomOAuthServer.Infrastructure.Security;

public sealed class AuditService(NpgsqlDataSource dataSource, ILogger<AuditService> logger) : IAuditService
{
    public async Task WriteAsync(
        string eventType,
        string? actorSubject,
        string? target,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var detailsJson = details is null ? null : JsonSerializer.Serialize(details);

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO audit_events (event_type, actor_subject, target, details)
                VALUES (@eventType, @actorSubject, @target, @details::jsonb)
                """;
            cmd.Parameters.AddWithValue("eventType", eventType);
            cmd.Parameters.AddWithValue("actorSubject", (object?)actorSubject ?? DBNull.Value);
            cmd.Parameters.AddWithValue("target", (object?)target ?? DBNull.Value);
            cmd.Parameters.AddWithValue("details", (object?)detailsJson ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write audit event {EventType} for {Target}", eventType, target);
        }
    }
}
