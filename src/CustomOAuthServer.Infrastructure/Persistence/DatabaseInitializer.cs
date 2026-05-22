using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.OAuth;
using CustomOAuthServer.Application.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CustomOAuthServer.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ApplicationDbContext dbContext,
    NpgsqlDataSource dataSource,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IHostEnvironment environment,
    IOptions<SeedOptions> seedOptions,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseExistsAsync(cancellationToken);

        if (environment.IsProduction())
        {
            await ValidateSqlSchemaAsync(cancellationToken);
            await ValidateOpenIddictSchemaAsync(cancellationToken);
        }
        else
        {
            await ApplyOpenIddictSchemaAsync(cancellationToken);
        }

        if (seedOptions.Value.Enabled)
        {
            await SeedOpenIddictAsync(cancellationToken);
            await SeedUsersAsync(cancellationToken);
        }
        else
        {
            logger.LogInformation("Startup seeding is disabled (Seed:Enabled=false)");
        }

        logger.LogInformation("Database initialization completed");
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        try
        {
            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var existsCmd = connection.CreateCommand();
            existsCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            existsCmd.Parameters.AddWithValue("name", databaseName);
            var exists = await existsCmd.ExecuteScalarAsync(cancellationToken) is not null;
            if (exists)
            {
                return;
            }

            await using var createCmd = connection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{databaseName.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Created database {Database}", databaseName);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P04")
        {
            // Database already exists (race).
        }
    }

    private async Task ValidateSqlSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'app_users'
            LIMIT 1
            """;
        if (await cmd.ExecuteScalarAsync(cancellationToken) is null)
        {
            throw new InvalidOperationException(
                "Table 'app_users' is missing. Run database migrations before starting the API: " +
                "docker compose run --rm migrate, or ./scripts/run-database-migrations.sh");
        }
    }

    private async Task ValidateOpenIddictSchemaAsync(CancellationToken cancellationToken)
    {
        if (!await OpenIddictSchemaExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "OpenIddict database schema is missing. Run migrations before deploy: " +
                "docker compose run --rm migrate, or ./scripts/run-database-migrations.sh");
        }
    }

    private async Task ApplyOpenIddictSchemaAsync(CancellationToken cancellationToken)
    {
        var dbOpts = databaseOptions.Value;
        var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Applied OpenIddict EF migrations: {Migrations}", string.Join(", ", pending));
        }

        if (await OpenIddictSchemaExistsAsync(cancellationToken))
        {
            return;
        }

        if (dbOpts.AllowEnsureCreatedFallback)
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogWarning(
                "OpenIddict schema created via EnsureCreated (development fallback only). " +
                "Generate EF migrations before production: ./scripts/generate-openiddict-migrations.sh InitialOpenIddict");
            return;
        }

        if (dbOpts.RequireOpenIddictSchema)
        {
            throw new InvalidOperationException(
                "OpenIddict schema is not present. Set Database:AllowEnsureCreatedFallback=true for local dev, " +
                "or apply EF migrations for production.");
        }
    }

    private async Task<bool> OpenIddictSchemaExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'OpenIddictApplications'
                LIMIT 1
                """;
            return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task SeedOpenIddictAsync(CancellationToken cancellationToken)
    {
        if (await scopeManager.FindByNameAsync("api", cancellationToken) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                DisplayName = "Custom OAuth API",
                Resources = { "resource_server" }
            }, cancellationToken);
        }

        if (await scopeManager.FindByNameAsync("admin", cancellationToken) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "admin",
                DisplayName = "Admin API",
                Resources = { "resource_server" }
            }, cancellationToken);
        }

        if (await applicationManager.FindByClientIdAsync("spa-client", cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "spa-client",
                DisplayName = "SPA / Native (PKCE)",
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                RedirectUris =
                {
                    new Uri("https://localhost:3000/callback"),
                    new Uri("http://127.0.0.1:7890/callback")
                },
                PostLogoutRedirectUris = { new Uri("https://localhost:3000/") },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,
                    Permissions.Prefixes.Endpoint + "userinfo",
                    Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Prefixes.Scope + Scopes.OpenId,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                    Permissions.Prefixes.Scope + "api"
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            }, cancellationToken);
        }

        if (await applicationManager.FindByClientIdAsync("m2m-client", cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "m2m-client",
                ClientSecret = GetClientSecret("m2m-client"),
                DisplayName = "Machine-to-Machine",
                ClientType = ClientTypes.Confidential,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Introspection,
                    Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + "api"
                }
            }, cancellationToken);
        }

        if (await applicationManager.FindByClientIdAsync("obo-client", cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "obo-client",
                ClientSecret = GetClientSecret("obo-client"),
                DisplayName = "On-Behalf-Of downstream API",
                ClientType = ClientTypes.Confidential,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Revocation,
                    Permissions.Prefixes.GrantType + OAuthGrantTypes.TokenExchange,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + "api"
                }
            }, cancellationToken);
        }

        if (await applicationManager.FindByClientIdAsync("introspection-client", cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "introspection-client",
                ClientSecret = GetClientSecret("introspection-client"),
                DisplayName = "Token introspection (resource server)",
                ClientType = ClientTypes.Confidential,
                Permissions =
                {
                    Permissions.Endpoints.Introspection
                }
            }, cancellationToken);
        }

        if (await applicationManager.FindByClientIdAsync("admin-client", cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "admin-client",
                ClientSecret = GetClientSecret("admin-client"),
                DisplayName = "Admin API (user & client management)",
                ClientType = ClientTypes.Confidential,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + "admin"
                }
            }, cancellationToken);
        }

        await UpgradeExistingApplicationsAsync(cancellationToken);
    }

    private string GetClientSecret(string clientId)
    {
        var options = seedOptions.Value;
        if (options.ClientSecrets.TryGetValue(clientId, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (environment.IsDevelopment())
        {
            if (DevelopmentSeedSecrets.ClientSecrets.TryGetValue(clientId, out var devSecret))
            {
                logger.LogWarning(
                    "Using development-only secret for client {ClientId}. Configure Seed:ClientSecrets in Production.",
                    clientId);
                return devSecret;
            }
        }

        throw new InvalidOperationException(
            $"Seed client secret for '{clientId}' is not configured. " +
            $"Set Seed__ClientSecrets__{clientId} or Seed:ClientSecrets:{clientId} via environment/configuration.");
    }

    private async Task UpgradeExistingApplicationsAsync(CancellationToken cancellationToken)
    {
        await EnsureClientPermissionsAsync("spa-client", descriptor =>
        {
            AddPermission(descriptor, Permissions.Endpoints.Authorization);
            AddPermission(descriptor, Permissions.Endpoints.Token);
            AddPermission(descriptor, Permissions.Endpoints.EndSession);
            AddPermission(descriptor, Permissions.Prefixes.Endpoint + "userinfo");
            AddPermission(descriptor, Permissions.Endpoints.Revocation);
            AddPermission(descriptor, Permissions.GrantTypes.AuthorizationCode);
            AddPermission(descriptor, Permissions.GrantTypes.RefreshToken);
            AddPermission(descriptor, Permissions.ResponseTypes.Code);
            AddPermission(descriptor, Permissions.Prefixes.Scope + Scopes.OpenId);
            AddPermission(descriptor, Permissions.Scopes.Email);
            AddPermission(descriptor, Permissions.Scopes.Profile);
            AddPermission(descriptor, Permissions.Scopes.Roles);
            AddPermission(descriptor, Permissions.Prefixes.Scope + Scopes.OfflineAccess);
            AddPermission(descriptor, Permissions.Prefixes.Scope + "api");
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }, cancellationToken);

        await EnsureClientPermissionsAsync("m2m-client", descriptor =>
        {
            AddPermission(descriptor, Permissions.Endpoints.Token);
            AddPermission(descriptor, Permissions.Endpoints.Introspection);
            AddPermission(descriptor, Permissions.Endpoints.Revocation);
            AddPermission(descriptor, Permissions.GrantTypes.ClientCredentials);
            AddPermission(descriptor, Permissions.Prefixes.Scope + "api");
        }, cancellationToken);

        await EnsureClientPermissionsAsync("obo-client", descriptor =>
        {
            AddPermission(descriptor, Permissions.Endpoints.Token);
            AddPermission(descriptor, Permissions.Endpoints.Revocation);
            AddPermission(descriptor, Permissions.Prefixes.GrantType + OAuthGrantTypes.TokenExchange);
            AddPermission(descriptor, Permissions.GrantTypes.ClientCredentials);
            AddPermission(descriptor, Permissions.Prefixes.Scope + "api");
        }, cancellationToken);

        await EnsureClientPermissionsAsync("admin-client", descriptor =>
        {
            AddPermission(descriptor, Permissions.Endpoints.Token);
            AddPermission(descriptor, Permissions.GrantTypes.ClientCredentials);
            AddPermission(descriptor, Permissions.Prefixes.Scope + "admin");
        }, cancellationToken);
    }

    private async Task EnsureClientPermissionsAsync(
        string clientId,
        Action<OpenIddictApplicationDescriptor> configure,
        CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        configure(descriptor);
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        logger.LogInformation("Updated permissions for client {ClientId}", clientId);
    }

    private static void AddPermission(OpenIddictApplicationDescriptor descriptor, string permission)
    {
        if (!descriptor.Permissions.Contains(permission, StringComparer.Ordinal))
        {
            descriptor.Permissions.Add(permission);
        }
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        var options = seedOptions.Value;
        if (!options.DemoUsers)
        {
            return;
        }

        var demoPassword = options.DemoUserPassword;
        if (string.IsNullOrWhiteSpace(demoPassword) && environment.IsDevelopment())
        {
            demoPassword = DevelopmentSeedSecrets.DemoUserPassword;
            logger.LogWarning("Using development-only demo user password. Set Seed:DemoUserPassword for non-development environments.");
        }

        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoUsers is true but no DemoUserPassword configured; skipping demo user seed.");
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM app_users";
        var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
        if (count > 0)
        {
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword);
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO app_users (id, username, email, display_name, password_hash)
            VALUES
                ('user-alice', 'alice', 'alice@example.com', 'Alice Example', @hash),
                ('user-bob', 'bob', 'bob@example.com', 'Bob Example', @hash)
            """;
        insert.Parameters.AddWithValue("hash", passwordHash);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Seeded demo users alice/bob");
    }
}
