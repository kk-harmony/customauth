using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomOAuthServer.Infrastructure.Persistence;

/// <summary>
/// Ensures OpenIddict EF tables exist (MigrateAsync or EnsureCreated when no migrations are committed).
/// </summary>
public static class OpenIddictSchemaBootstrapper
{
    public static async Task EnsureAsync(
        ApplicationDbContext dbContext,
        ILogger? logger,
        bool allowEnsureCreated = true,
        CancellationToken cancellationToken = default)
    {
        if (await SchemaExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger?.LogInformation(
                "Applied OpenIddict EF migrations: {Migrations}",
                string.Join(", ", pending));
            return;
        }

        var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count > 0)
        {
            throw new InvalidOperationException(
                "OpenIddict schema is missing but EF migration history exists. "
                + "Restore the database or add a new EF migration.");
        }

        if (!allowEnsureCreated)
        {
            throw new InvalidOperationException(
                "OpenIddict schema is not present. Set Database:AllowEnsureCreatedFallback=true for local dev, "
                + "or run Migrations/Sql/003_openiddict.sql for production.");
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        logger?.LogWarning(
            "OpenIddict schema created via EnsureCreated. Run 'dotnet ef migrations add' and redeploy for versioned schema.");
    }

    public static async Task<bool> SchemaExistsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'OpenIddictApplications'
            LIMIT 1
            """;
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }
}
