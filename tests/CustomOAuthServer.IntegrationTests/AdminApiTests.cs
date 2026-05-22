using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CustomOAuthServer.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AdminApiTests(IntegrationTestFixture fixture)
{
    [SkippableFact]
    public async Task Admin_users_and_clients_crud_requires_admin_scope()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var anonymous = fixture.CreateClient();
        (await anonymous.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var apiOnly = fixture.CreateClient();
        var apiToken = await OAuthTestHelper.GetClientCredentialsTokenAsync(apiOnly);
        using var apiClient = OAuthTestHelper.CreateAuthorizedClient(fixture, apiToken);
        (await apiClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var tokenClient = fixture.CreateClient();
        var adminToken = await OAuthTestHelper.GetAdminTokenAsync(tokenClient);
        using var admin = OAuthTestHelper.CreateAuthorizedClient(fixture, adminToken);

        var createUser = new
        {
            username = $"testuser_{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@example.com",
            password = "TestPassword123!@",
            displayName = "Test User"
        };

        var createUserResponse = await admin.PostAsJsonAsync("/api/admin/users", createUser);
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = createdUser.GetProperty("id").GetString()!;

        (await admin.GetAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateUserResponse = await admin.PutAsJsonAsync(
            $"/api/admin/users/{userId}",
            new { displayName = "Updated Name" });
        updateUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var testClientId = $"test-client-{Guid.NewGuid():N}";
        var createClient = new
        {
            clientId = testClientId,
            displayName = "Integration Test Client",
            clientType = "confidential",
            clientSecret = "test-secret-value",
            grantTypes = new[] { "client_credentials" },
            scopes = new[] { "api" }
        };

        var createClientResponse = await admin.PostAsJsonAsync("/api/admin/clients", createClient);
        createClientResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        (await admin.GetAsync($"/api/admin/clients/{testClientId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateClientResponse = await admin.PutAsJsonAsync(
            $"/api/admin/clients/{testClientId}",
            new { displayName = "Updated Client" });
        updateClientResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.DeleteAsync($"/api/admin/clients/{testClientId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/admin/clients/{testClientId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.DeleteAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteProtected = await admin.DeleteAsync("/api/admin/clients/admin-client");
        deleteProtected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
