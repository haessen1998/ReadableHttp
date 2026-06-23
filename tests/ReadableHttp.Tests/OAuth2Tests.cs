using ReadableHttp.Auth;
using ReadableHttp;
using System.Net;
using System.Text;

namespace ReadableHttp.Tests;

public sealed class OAuth2Tests
{
    [Fact]
    public void Pkce_Create_returns_verifier_and_s256_challenge()
    {
        var pkce = OAuth2Pkce.Create();

        Assert.NotEmpty(pkce.CodeVerifier);
        Assert.NotEmpty(pkce.CodeChallenge);
        Assert.Equal("S256", pkce.Method);
        Assert.DoesNotContain("+", pkce.CodeVerifier);
        Assert.DoesNotContain("/", pkce.CodeVerifier);
    }

    [Fact]
    public void AuthorizationRequest_builds_authorization_code_url()
    {
        var request = OAuth2AuthorizationRequest.FromOptions(
            new ReadableOAuth2Options
            {
                AuthorizationUrl = "https://login.example.test/authorize",
                ClientId = "client",
                Scopes = ["openid", "profile"],
                UsePkce = true
            },
            "http://localhost:51000/callback",
            "state");

        var uri = request.ToUri().ToString();

        Assert.Contains("response_type=code", uri);
        Assert.Contains("client_id=client", uri);
        Assert.Contains("redirect_uri=http%3a%2f%2flocalhost%3a51000%2fcallback", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scope=openid+profile", uri);
        Assert.Contains("state=state", uri);
        Assert.Contains("code_challenge=", uri);
        Assert.Contains("code_challenge_method=S256", uri);
    }

    [Fact]
    public async Task TokenClient_requests_client_credentials_with_basic_auth()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{\"access_token\":\"abc\",\"token_type\":\"Bearer\",\"expires_in\":3600}")));
        var client = new OAuth2TokenClient(new HttpClient(handler));

        var token = await client.ClientCredentialsAsync(
            new ReadableOAuth2Options
            {
                TokenUrl = "https://login.example.test/oauth/token",
                ClientId = "client",
                ClientSecret = "secret",
                ClientAuthentication = ReadableOAuth2ClientAuthentication.ClientSecretBasic,
                Scopes = ["api"]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("abc", token.AccessToken);
        Assert.Equal("Basic", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("client:secret")), handler.Requests[0].Headers.Authorization?.Parameter);
        var body = await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("grant_type=client_credentials", body);
        Assert.Contains("scope=api", body);
    }

    [Fact]
    public async Task TokenClient_requests_password_credentials_and_refresh_url()
    {
        var calls = 0;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            calls++;
            var content = calls == 1
                ? "{\"access_token\":\"password-token\"}"
                : "{\"access_token\":\"refresh-token\",\"refresh_token\":\"next\"}";
            return Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, content));
        });
        var client = new OAuth2TokenClient(new HttpClient(handler));
        var options = new ReadableOAuth2Options
        {
            TokenUrl = "https://login.example.test/token",
            RefreshTokenUrl = "https://login.example.test/refresh",
            ClientId = "client",
            ClientSecret = "secret",
            ClientAuthentication = ReadableOAuth2ClientAuthentication.ClientSecretPost,
            Username = "demo",
            Password = "pass"
        };

        var password = await client.PasswordCredentialsAsync(options, TestContext.Current.CancellationToken);
        var refresh = await client.RefreshAsync(options, "old", TestContext.Current.CancellationToken);

        Assert.Equal("password-token", password.AccessToken);
        Assert.Equal("refresh-token", refresh.AccessToken);
        Assert.Equal("https://login.example.test/refresh", handler.Requests[1].RequestUri?.ToString());
        var passwordBody = await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("grant_type=password", passwordBody);
        Assert.Contains("username=demo", passwordBody);
        Assert.Contains("client_secret=secret", passwordBody);
    }

    [Fact]
    public void TokenCache_tracks_expiry_and_token_source()
    {
        var cache = new OAuth2TokenCache();
        var issuedAt = DateTimeOffset.UtcNow;

        var cached = cache.Set("credentials", new OAuth2TokenResponse
        {
            AccessToken = "access",
            IdToken = "id",
            RefreshToken = "refresh",
            ExpiresIn = 120
        }, issuedAt);

        Assert.Same(cached, cache.GetUsable("credentials", refreshBeforeExpirySeconds: 30));
        Assert.Null(cache.GetUsable("credentials", refreshBeforeExpirySeconds: 180));
        Assert.Equal("id", cached.GetToken(ReadableOAuth2TokenSource.IdToken));
        Assert.False(cached.IsExpired(issuedAt.AddSeconds(60)));
        Assert.True(cached.IsExpired(issuedAt.AddSeconds(121)));
    }
}
