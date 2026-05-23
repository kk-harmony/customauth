using System.Threading.RateLimiting;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.HttpOverrides;
using CustomOAuthServer.Api.Authorization;
using CustomOAuthServer.Api.Configuration;
using CustomOAuthServer.Api.Cors;
using CustomOAuthServer.Api.Middleware;
using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Options;
using CustomOAuthServer.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    var serilogOptions = context.Configuration.GetSection(SerilogOptions.SectionName).Get<SerilogOptions>()
        ?? new SerilogOptions();

    var level = Enum.TryParse<LogEventLevel>(serilogOptions.MinimumLevel, true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Is(level)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "CustomOAuthServer")
        .WriteTo.Console()
        .WriteTo.File(
            serilogOptions.LogFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<OAuthServerOptions>(
    builder.Configuration.GetSection(OAuthServerOptions.SectionName));
builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection(ConnectionStringsOptions.SectionName));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/account/login";
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAdminAuthorization();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

var oauthOptions = builder.Configuration.GetSection(OAuthServerOptions.SectionName).Get<OAuthServerOptions>()
    ?? new OAuthServerOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyConfigurator.DefaultPolicyName, policy =>
        CorsPolicyConfigurator.ConfigureDefaultPolicy(policy, oauthOptions, builder.Environment));
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = oauthOptions.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.GeneralRules =
    [
        new RateLimitRule { Endpoint = "POST:/connect/token", Period = "1m", Limit = 60 },
        new RateLimitRule { Endpoint = "POST:/account/login", Period = "1m", Limit = 20 }
    ];
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

ProductionConfigurationValidator.ValidateIfProduction(app.Environment);

app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId",
            httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? "n/a");

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value;
            if (userId is not null)
            {
                diagnosticContext.Set("UserId", userId);
            }
        }
    };
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseIpRateLimiting();
app.UseRateLimiter();
app.UseCors(CorsPolicyConfigurator.DefaultPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow }))
    .AllowAnonymous()
    .WithName("Liveness");

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        });
    }
}).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/.well-known/openid-configuration"))
    .AllowAnonymous();

await app.RunAsync();

namespace CustomOAuthServer.Api
{
    public partial class Program;
}
