using CustomOAuthServer.Application.Admin;

namespace CustomOAuthServer.Application.Abstractions;

public interface IClientAdminService
{
    Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<ClientResponse?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);
    Task<ClientResponse?> UpdateAsync(string clientId, UpdateClientRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string clientId, CancellationToken cancellationToken = default);
}
