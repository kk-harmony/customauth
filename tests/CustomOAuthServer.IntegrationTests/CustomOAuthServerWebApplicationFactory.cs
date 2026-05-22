using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CustomOAuthServer.IntegrationTests;

public sealed class CustomOAuthServerWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("OAuthServer__Issuer", "http://localhost/");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);

        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("OAuthServer:Issuer", "http://localhost/");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["OAuthServer:Issuer"] = "http://localhost/",
                ["Serilog:MinimumLevel"] = "Warning",
                ["Database:AllowEnsureCreatedFallback"] = "true",
                ["Seed:Enabled"] = "true",
                ["Seed:DemoUsers"] = "true",
                ["Seed:DemoUserPassword"] = "Password123!",
                ["Seed:ClientSecrets:m2m-client"] = OAuthTestHelper.M2MClientSecret,
                ["Seed:ClientSecrets:obo-client"] = OAuthTestHelper.OboClientSecret,
                ["Seed:ClientSecrets:introspection-client"] = OAuthTestHelper.IntrospectionClientSecret,
                ["Seed:ClientSecrets:admin-client"] = OAuthTestHelper.AdminClientSecret
            });
        });
    }
}
