using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomOAuthServer.Api.Controllers;

[ApiController]
[Route("api/admin/clients")]
[Authorize(Policy = AdminPolicies.PolicyName)]
public sealed class AdminClientsController(IClientAdminService clientAdminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var clients = await clientAdminService.ListAsync(cancellationToken);
        return Ok(clients);
    }

    [HttpGet("{clientId}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string clientId, CancellationToken cancellationToken)
    {
        var client = await clientAdminService.GetByClientIdAsync(clientId, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "clientId and displayName are required." });
        }

        try
        {
            var created = await clientAdminService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { clientId = created.ClientId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{clientId}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        string clientId,
        [FromBody] UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await clientAdminService.UpdateAsync(clientId, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string clientId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await clientAdminService.DeleteAsync(clientId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
