using CustomOAuthServer.Application.Abstractions;
using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.Security;
using OpenIddict.Abstractions;

namespace CustomOAuthServer.Infrastructure.Admin;

public sealed class ClientAdminService(
    IOpenIddictApplicationManager applicationManager,
    IAuditService auditService) : IClientAdminService
{
    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ClientResponse>();

        await foreach (var application in applicationManager.ListAsync(cancellationToken: cancellationToken))
        {
            var clientId = await applicationManager.GetClientIdAsync(application, cancellationToken);
            if (clientId is null)
            {
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
            results.Add(ClientDescriptorBuilder.ToResponse(descriptor, clientId));
        }

        return results.OrderBy(c => c.ClientId).ToList();
    }

    public async Task<ClientResponse?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        return ClientDescriptorBuilder.ToResponse(descriptor, clientId);
    }

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Client '{request.ClientId}' already exists.");
        }

        var descriptor = ClientDescriptorBuilder.BuildDescriptor(request);
        await applicationManager.CreateAsync(descriptor, cancellationToken);
        var created = ClientDescriptorBuilder.ToResponse(descriptor, request.ClientId);
        await auditService.WriteAsync("client.created", null, request.ClientId, created, cancellationToken);
        return created;
    }

    public async Task<ClientResponse?> UpdateAsync(
        string clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        ClientDescriptorBuilder.ApplyUpdate(descriptor, request);
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        var updated = ClientDescriptorBuilder.ToResponse(descriptor, clientId);
        await auditService.WriteAsync("client.updated", null, clientId, request, cancellationToken);
        return updated;
    }

    public async Task<bool> DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (SystemClients.IsProtected(clientId))
        {
            throw new InvalidOperationException($"Client '{clientId}' is a protected system client and cannot be deleted.");
        }

        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        await applicationManager.DeleteAsync(application, cancellationToken);
        await auditService.WriteAsync("client.deleted", null, clientId, cancellationToken: cancellationToken);
        return true;
    }
}
