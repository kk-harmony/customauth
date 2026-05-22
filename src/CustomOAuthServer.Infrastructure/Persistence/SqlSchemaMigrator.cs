using Npgsql;

namespace CustomOAuthServer.Infrastructure.Persistence;

/// <summary>
/// Applies versioned SQL scripts from Migrations/Sql. Used by integration tests and deployment scripts — not at API startup.
/// </summary>
public static class SqlSchemaMigrator
{
    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var scriptsPath = ResolveSqlScriptsPath()
            ?? throw new InvalidOperationException(
                "SQL migrations directory not found. Expected Migrations/Sql relative to the repository or output directory.");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    name TEXT PRIMARY KEY,
                    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var file in Directory.GetFiles(scriptsPath, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT 1 FROM schema_migrations WHERE name = @name";
            check.Parameters.AddWithValue("name", name);
            if (await check.ExecuteScalarAsync(cancellationToken) is not null)
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            await using var batch = connection.CreateCommand();
            batch.CommandText = sql;
            await batch.ExecuteNonQueryAsync(cancellationToken);

            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO schema_migrations (name) VALUES (@name)";
            insert.Parameters.AddWithValue("name", name);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static string? ResolveSqlScriptsPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Migrations", "Sql"),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Migrations", "Sql")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Migrations", "Sql")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Migrations", "Sql"))
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
