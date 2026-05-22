using System.Diagnostics;
using CustomOAuthServer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CustomOAuthServer.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private CustomOAuthServerWebApplicationFactory? _factory;

    public string? SkipReason { get; private set; }

    public CustomOAuthServerWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture was not initialized.");

    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void EnsureAvailable()
    {
        if (SkipReason is not null)
        {
            throw new InvalidOperationException(SkipReason);
        }
    }

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CUSTOMOAUTH_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = await TryStartTestcontainerAsync()
                ?? await TryLocalPostgresAsync();
        }

        if (connectionString is null)
        {
            SkipReason =
                "PostgreSQL is not available. Start Docker Desktop, run 'docker compose up -d postgres', " +
                "or set CUSTOMOAUTH_TEST_CONNECTION to a valid connection string.";
            return;
        }

        if (!await CanConnectAsync(connectionString))
        {
            SkipReason = $"Cannot connect to PostgreSQL using: {SanitizeConnectionString(connectionString)}";
            return;
        }

        try
        {
            await SqlSchemaMigrator.ApplyAsync(connectionString);
        }
        catch (Exception ex)
        {
            SkipReason = $"Failed to apply SQL schema migrations: {ex.Message}";
            return;
        }

        _factory = new CustomOAuthServerWebApplicationFactory(connectionString);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task<string?> TryStartTestcontainerAsync()
    {
        if (!IsDockerAvailable())
        {
            return null;
        }

        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("customoauth_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();
            return _container.GetConnectionString();
        }
        catch
        {
            _container = null;
            return null;
        }
    }

    private static async Task<string?> TryLocalPostgresAsync()
    {
        const string local =
            "Host=localhost;Port=5432;Database=customoauth;Username=postgres;Password=postgres";

        return await CanConnectAsync(local) ? local : null;
    }

    private static async Task<bool> CanConnectAsync(string connectionString)
    {
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "***";
            }

            return builder.ToString();
        }
        catch
        {
            return "(invalid connection string)";
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return process is not null && process.WaitForExit(3000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
