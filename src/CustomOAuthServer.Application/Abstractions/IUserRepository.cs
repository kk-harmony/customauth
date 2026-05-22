using CustomOAuthServer.Application.Admin;
using CustomOAuthServer.Application.Models;

namespace CustomOAuthServer.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse?> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
