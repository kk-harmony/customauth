using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CustomOAuthServer.Application.OAuth;
using FluentAssertions;
using Xunit;

namespace CustomOAuthServer.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class OAuthFlowTests(IntegrationTestFixture fixture)
{
    [SkippableFact]
    public async Task Discovery_document_lists_core_endpoints()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        doc.GetProperty("issuer").GetString().Should().Be("http://localhost/");
        doc.GetProperty("authorization_endpoint").GetString().Should().Contain("/connect/authorize");
        doc.GetProperty("token_endpoint").GetString().Should().Contain("/connect/token");
        doc.GetProperty("userinfo_endpoint").GetString().Should().Contain("/connect/userinfo");
        doc.GetProperty("revocation_endpoint").GetString().Should().Contain("/connect/revoke");
        doc.GetProperty("introspection_endpoint").GetString().Should().Contain("/connect/introspect");
    }

    [SkippableFact]
    public async Task Authorization_code_pkce_returns_access_and_refresh_tokens()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var client = OAuthTestHelper.CreateCookieClient(fixture);
        var (accessToken, refreshToken) = await OAuthTestHelper.AuthorizeWithPkceAsync(client, verifier, challenge);

        accessToken.Should().NotBeNullOrEmpty();
        refreshToken.Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task Refresh_token_grant_returns_new_access_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var client = OAuthTestHelper.CreateCookieClient(fixture);
        var (_, refreshToken) = await OAuthTestHelper.AuthorizeWithPkceAsync(client, verifier, challenge);

        var refreshed = await OAuthTestHelper.RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "spa-client",
            ["refresh_token"] = refreshToken
        });

        refreshed.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task Token_exchange_obo_returns_delegated_access_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var cookieClient = OAuthTestHelper.CreateCookieClient(fixture);
        var (userAccessToken, _) = await OAuthTestHelper.AuthorizeWithPkceAsync(cookieClient, verifier, challenge);

        using var client = fixture.CreateClient();
        var exchanged = await OAuthTestHelper.ExchangeUserTokenAsync(
            client,
            userAccessToken,
            audience: "resource_server");

        var exchangedAccessToken = exchanged.GetProperty("access_token").GetString();
        exchangedAccessToken.Should().NotBeNullOrEmpty();

        var introspection = await OAuthTestHelper.IntrospectTokenAsync(
            client,
            exchangedAccessToken!,
            clientId: "obo-client",
            clientSecret: OAuthTestHelper.OboClientSecret);
        introspection.GetProperty("active").GetBoolean().Should().BeTrue();
        introspection.GetProperty("sub").GetString().Should().Be("user-alice");
        introspection.GetProperty("aud").GetString().Should().Be("resource_server");
        introspection.GetProperty("client_id").GetString().Should().Be("obo-client");
    }

    [SkippableFact]
    public async Task Token_exchange_rejects_client_credentials_subject()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var subjectToken = await OAuthTestHelper.GetClientCredentialsTokenAsync(client);

        var (statusCode, body) = await OAuthTestHelper.RequestTokenWithStatusAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = OAuthGrantTypes.TokenExchange,
            ["client_id"] = "obo-client",
            ["client_secret"] = OAuthTestHelper.OboClientSecret,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = OAuthTokenTypes.AccessToken,
            ["audience"] = "resource_server",
            ["scope"] = "api"
        });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().Should().Be("invalid_grant");
    }

    [SkippableFact]
    public async Task Token_exchange_requires_audience()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var cookieClient = OAuthTestHelper.CreateCookieClient(fixture);
        var (userAccessToken, _) = await OAuthTestHelper.AuthorizeWithPkceAsync(cookieClient, verifier, challenge);

        using var client = fixture.CreateClient();
        var (statusCode, body) = await OAuthTestHelper.RequestTokenWithStatusAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = OAuthGrantTypes.TokenExchange,
            ["client_id"] = "obo-client",
            ["client_secret"] = OAuthTestHelper.OboClientSecret,
            ["subject_token"] = userAccessToken,
            ["subject_token_type"] = OAuthTokenTypes.AccessToken,
            ["scope"] = "api"
        });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().Should().Be("invalid_request");
    }

    [SkippableFact]
    public async Task Token_exchange_rejects_disallowed_audience()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var cookieClient = OAuthTestHelper.CreateCookieClient(fixture);
        var (userAccessToken, _) = await OAuthTestHelper.AuthorizeWithPkceAsync(cookieClient, verifier, challenge);

        using var client = fixture.CreateClient();
        var (statusCode, body) = await OAuthTestHelper.RequestTokenWithStatusAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = OAuthGrantTypes.TokenExchange,
            ["client_id"] = "obo-client",
            ["client_secret"] = OAuthTestHelper.OboClientSecret,
            ["subject_token"] = userAccessToken,
            ["subject_token_type"] = OAuthTokenTypes.AccessToken,
            ["audience"] = "unknown-api",
            ["scope"] = "api"
        });

        statusCode.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().Should().Be("invalid_target");
    }

    [SkippableFact]
    public async Task Introspection_returns_active_for_valid_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var accessToken = await OAuthTestHelper.GetClientCredentialsTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = "m2m-client",
            ["client_secret"] = OAuthTestHelper.M2MClientSecret
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "introspection error: {0}", body);

        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("active").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Userinfo_returns_profile_for_user_access_token()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        var (verifier, challenge) = OAuthTestHelper.CreatePkcePair();
        using var client = OAuthTestHelper.CreateCookieClient(fixture);
        var (accessToken, _) = await OAuthTestHelper.AuthorizeWithPkceAsync(client, verifier, challenge);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("sub").GetString().Should().Be("user-alice");
    }

    [SkippableFact]
    public async Task Revocation_makes_token_inactive_on_introspection()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason);

        using var client = fixture.CreateClient();
        var accessToken = await OAuthTestHelper.GetClientCredentialsTokenAsync(client);

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/revoke");
        revokeRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["token_type_hint"] = "access_token",
            ["client_id"] = "m2m-client",
            ["client_secret"] = OAuthTestHelper.M2MClientSecret
        });
        var revokeResponse = await client.SendAsync(revokeRequest);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var introspectRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        introspectRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = "m2m-client",
            ["client_secret"] = OAuthTestHelper.M2MClientSecret
        });
        var introspectResponse = await client.SendAsync(introspectRequest);
        var json = await introspectResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("active").GetBoolean().Should().BeFalse();
    }
}
