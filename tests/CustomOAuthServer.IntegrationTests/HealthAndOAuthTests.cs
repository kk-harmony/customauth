using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CustomOAuthServer.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class HealthAndOAuthTests(IntegrationTestFixture fixture)
{
    [SkippableFact]
    public async Task Health_returns_200()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [SkippableFact]
    public async Task Ready_returns_200_when_database_is_up()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Client_credentials_grant_returns_access_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var json = await OAuthTestHelper.RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "m2m-client",
            ["client_secret"] = OAuthTestHelper.M2MClientSecret,
            ["scope"] = "api"
        });

        json.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("token_type").GetString().Should().Be("Bearer");
    }

    [SkippableFact]
    public async Task Api_me_requires_authentication()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Api_me_returns_user_with_valid_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var token = await OAuthTestHelper.GetClientCredentialsTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("sub").GetString().Should().Be("m2m-client");
    }
}
