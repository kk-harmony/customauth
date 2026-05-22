using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Options;
using CustomOAuthServer.Infrastructure.Admin;
using CustomOAuthServer.Infrastructure.Health;
using CustomOAuthServer.Infrastructure.OpenIddict;
using CustomOAuthServer.Infrastructure.Persistence;
using CustomOAuthServer.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CustomOAuthServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetSection(ConnectionStringsOptions.SectionName)
            .Get<ConnectionStringsOptions>()?.DefaultConnection
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseOpenIddict();
        });

        services.AddHealthChecks()
            .AddCheck<NpgsqlHealthCheck>("postgresql", tags: ["ready"]);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClientAdminService, ClientAdminService>();
        services.AddScoped<ILoginProtectionService, LoginProtectionService>();
        services.AddScoped<IPasswordPolicyValidator, PasswordPolicyValidator>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SectionName);

        services.AddCustomOpenIddictServer(configuration, environment);

        return services;
    }
}
