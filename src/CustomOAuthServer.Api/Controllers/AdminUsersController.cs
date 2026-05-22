using System.Security.Claims;
using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AdminPolicies.PolicyName)]
public sealed class AdminUsersController(
    IUserRepository userRepository,
    IPasswordPolicyValidator passwordPolicyValidator,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserResponse(user.Id, user.Username, user.Email, user.DisplayName, user.CreatedAt));
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "username and email are required." });
        }

        var passwordErrors = passwordPolicyValidator.Validate(request.Password);
        if (passwordErrors.Count > 0)
        {
            return BadRequest(new { error = "Password does not meet policy.", details = passwordErrors });
        }

        try
        {
            var created = await userRepository.CreateAsync(request, cancellationToken);
            await auditService.WriteAsync("user.created", GetActorSubject(), created.Id, new { created.Username }, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Password is not null)
        {
            var passwordErrors = passwordPolicyValidator.Validate(request.Password);
            if (passwordErrors.Count > 0)
            {
                return BadRequest(new { error = "Password does not meet policy.", details = passwordErrors });
            }
        }

        var updated = await userRepository.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        await auditService.WriteAsync("user.updated", GetActorSubject(), id, cancellationToken: cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await userRepository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditService.WriteAsync("user.deleted", GetActorSubject(), id, cancellationToken: cancellationToken);
        return NoContent();
    }

    private string? GetActorSubject() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
}
