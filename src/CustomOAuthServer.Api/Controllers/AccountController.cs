using System.Security.Claims;
using CustomOAuthServer.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
[Route("account")]
public sealed class AccountController(
    IUserRepository userRepository,
    ILoginProtectionService loginProtection,
    IAuditService auditService,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl ?? "/");
        return Content(AccountLoginPage.Render(encodedReturnUrl), "text/html");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> LoginPost(
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (await loginProtection.IsLockedOutAsync(username, cancellationToken))
        {
            logger.LogWarning("Blocked login attempt for locked account {Username}", username);
            return LoginFailed(returnUrl, username, "Account is temporarily locked. Try again later.");
        }

        if (!await userRepository.ValidateCredentialsAsync(username, password, cancellationToken))
        {
            await loginProtection.RegisterFailedAttemptAsync(username, cancellationToken);
            await auditService.WriteAsync("login.failed", null, username, cancellationToken: cancellationToken);
            logger.LogWarning("Failed login attempt for {Username}", username);
            return LoginFailed(returnUrl, username, "Invalid username or password.");
        }

        await loginProtection.RegisterSuccessfulLoginAsync(username, cancellationToken);

        var user = await userRepository.FindByUsernameAsync(username, cancellationToken)
            ?? throw new InvalidOperationException("User not found after validation.");

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        await auditService.WriteAsync("login.succeeded", user.Id, username, cancellationToken: cancellationToken);
        logger.LogInformation("User {UserId} signed in", user.Id);

        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "signed_out" });
    }

    private ContentResult LoginFailed(string? returnUrl, string username, string message) =>
        new()
        {
            Content = AccountLoginPage.Render(
                System.Net.WebUtility.HtmlEncode(returnUrl ?? "/"),
                message,
                System.Net.WebUtility.HtmlEncode(username)),
            ContentType = "text/html",
            StatusCode = StatusCodes.Status401Unauthorized
        };
}
