using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CustomOAuthServer.Application.OAuth;
using FluentAssertions;

namespace CustomOAuthServer.IntegrationTests;

internal static class OAuthTestHelper
{
    public const string SpaRedirectUri = "https://localhost:3000/callback";
    public const string M2MClientSecret = "m2m-secret-change-in-production";
    public const string OboClientSecret = "obo-secret-change-in-production";
    public const string IntrospectionClientSecret = "introspection-secret-change-in-production";
    public const string AdminClientSecret = "admin-secret-change-in-production";
    public const string DefaultAdminUsername = "admin";
    public const string DefaultAdminPassword = "ThisIsP@ss";
    public const string DefaultAdminSpaScopes = "openid profile api admin offline_access";

    public static HttpClient CreateCookieClient(IntegrationTestFixture fixture) =>
        fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    public static (string Verifier, string Challenge) CreatePkcePair()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64UrlEncode(verifierBytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static async Task<JsonElement> RequestTokenAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> form)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = new FormUrlEncodedContent(form);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Token request failed ({(int)response.StatusCode}): {body}");
        }

        return JsonDocument.Parse(body).RootElement;
    }

    public static async Task<(HttpStatusCode StatusCode, JsonElement Body)> RequestTokenWithStatusAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> form)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = new FormUrlEncodedContent(form);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(body)
            ? JsonDocument.Parse("{}").RootElement
            : JsonDocument.Parse(body).RootElement;
        return (response.StatusCode, json);
    }

    public static async Task<JsonElement> ExchangeUserTokenAsync(
        HttpClient client,
        string subjectToken,
        string audience,
        string scopes = "api")
    {
        return await RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = OAuthGrantTypes.TokenExchange,
            ["client_id"] = "obo-client",
            ["client_secret"] = OboClientSecret,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = OAuthTokenTypes.AccessToken,
            ["audience"] = audience,
            ["scope"] = scopes
        });
    }

    public static async Task<JsonElement> IntrospectTokenAsync(
        HttpClient client,
        string accessToken,
        string clientId = "m2m-client",
        string clientSecret = M2MClientSecret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "introspection error: {0}", body);
        return JsonDocument.Parse(body).RootElement;
    }

    public static async Task<string> GetDefaultAdminSpaTokenAsync(IntegrationTestFixture fixture)
    {
        using var client = CreateCookieClient(fixture);
        var (verifier, challenge) = CreatePkcePair();
        var (accessToken, _) = await AuthorizeWithPkceAsync(
            client,
            verifier,
            challenge,
            scopes: DefaultAdminSpaScopes,
            username: DefaultAdminUsername,
            password: DefaultAdminPassword);
        return accessToken;
    }

    public static async Task<string> GetAdminTokenAsync(HttpClient client) =>
        (await RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "admin-client",
            ["client_secret"] = AdminClientSecret,
            ["scope"] = "admin"
        })).GetProperty("access_token").GetString()!;

    public static HttpClient CreateAuthorizedClient(IntegrationTestFixture fixture, string accessToken)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<string> GetClientCredentialsTokenAsync(HttpClient client) =>
        (await RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "m2m-client",
            ["client_secret"] = M2MClientSecret,
            ["scope"] = "api"
        })).GetProperty("access_token").GetString()!;

    public static async Task<(string AccessToken, string RefreshToken)> AuthorizeWithPkceAsync(
        HttpClient client,
        string codeVerifier,
        string codeChallenge,
        string scopes = "openid profile api offline_access",
        string username = "alice",
        string password = "Password123!")
    {
        var returnPath = "/connect/authorize" +
            "?client_id=spa-client" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(scopes)}" +
            $"&redirect_uri={Uri.EscapeDataString(SpaRedirectUri)}" +
            $"&code_challenge={codeChallenge}" +
            "&code_challenge_method=S256";

        await LoginAsync(client, username, password);

        var authorizeResponse = await client.GetAsync(returnPath);
        if (NeedsLogin(authorizeResponse))
        {
            await LoginAsync(client, username, password);
            authorizeResponse = await client.GetAsync(returnPath);
        }

        var authorizeBody = await authorizeResponse.Content.ReadAsStringAsync();
        authorizeResponse.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther],
            "unexpected authorize status: {0}", authorizeBody);
        var location = authorizeResponse.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Missing authorization redirect.");
        var code = ExtractQueryParameter(location, "code")
            ?? throw new InvalidOperationException($"Authorization code not found in: {location}");

        var token = await RequestTokenAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "spa-client",
            ["code"] = code,
            ["redirect_uri"] = SpaRedirectUri,
            ["code_verifier"] = codeVerifier
        });

        return (
            token.GetProperty("access_token").GetString()!,
            token.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString()! : string.Empty);
    }

    public static string? ExtractQueryParameter(string url, string name)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            !Uri.TryCreate("https://local" + (url.StartsWith('?') ? url : "?" + url.Split('?').Last()), UriKind.Absolute, out uri))
        {
            return null;
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == name)
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        return null;
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["returnUrl"] = "/"
        });
        var loginResponse = await client.PostAsync("/account/login", loginForm);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther],
            "unexpected login status: {0}", loginBody);
    }

    private static bool NeedsLogin(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Unauthorized
        || (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther
            && response.Headers.Location?.ToString().Contains("/account/login", StringComparison.Ordinal) == true);

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
