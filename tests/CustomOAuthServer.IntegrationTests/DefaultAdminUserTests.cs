using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CustomOAuthServer.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DefaultAdminUserTests(IntegrationTestFixture fixture)
{
    [SkippableFact]
    public async Task Default_admin_user_can_crud_users_and_clients_via_spa_pkce()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var accessToken = await OAuthTestHelper.GetDefaultAdminSpaTokenAsync(fixture);
        using var admin = OAuthTestHelper.CreateAuthorizedClient(fixture, accessToken);

        var createUser = new
        {
            username = $"spaadmin_user_{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@example.com",
            password = "TestPassword123!@",
            displayName = "SPA Admin Created User"
        };

        var createUserResponse = await admin.PostAsJsonAsync("/api/admin/users", createUser);
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = createdUser.GetProperty("id").GetString()!;

        (await admin.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateUserResponse = await admin.PutAsJsonAsync(
            $"/api/admin/users/{userId}",
            new { displayName = "Updated By Default Admin" });
        updateUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var testClientId = $"spaadmin_client_{Guid.NewGuid():N}";
        var createClient = new
        {
            clientId = testClientId,
            displayName = "SPA Admin Created Client",
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
            new { displayName = "Updated Client By Default Admin" });
        updateClientResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.DeleteAsync($"/api/admin/clients/{testClientId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/admin/clients/{testClientId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.DeleteAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/admin/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteProtected = await admin.DeleteAsync("/api/admin/clients/admin-client");
        deleteProtected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Non_admin_user_cannot_manage_users_or_clients_via_spa_pkce()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = OAuthTestHelper.CreateCookieClient(fixture);
        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        var (accessToken, _) = await OAuthTestHelper.AuthorizeWithPkceAsync(
            client,
            verifier,
            challenge,
            scopes: OAuthTestHelper.DefaultAdminSpaScopes,
            username: "alice",
            password: "Password123!");

        using var api = OAuthTestHelper.CreateAuthorizedClient(fixture, accessToken);
        (await api.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.GetAsync("/api/admin/clients/spa-client")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createUserResponse = await api.PostAsJsonAsync("/api/admin/users", new
        {
            username = $"blocked_{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@example.com",
            password = "TestPassword123!@",
            displayName = "Should Fail"
        });
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
