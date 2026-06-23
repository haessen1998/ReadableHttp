using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using ReadableHttp.Core;

namespace ReadableHttp.Auth;

public sealed class OAuth2TokenClient
{
    private readonly HttpClient _httpClient;

    public OAuth2TokenClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<OAuth2TokenResponse> ExchangeAuthorizationCodeAsync(
        ReadableOAuth2Options options,
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.TokenUrl))
        {
            throw new ArgumentException("TokenUrl is required.", nameof(options));
        }

        var values = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", redirectUri),
        };

        AddClientCredentials(values, options);
        AddCommonExtraParameters(values, options);

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            values.Add(new KeyValuePair<string, string>("code_verifier", codeVerifier));
        }

        return await SendTokenRequestAsync(options, options.TokenUrl, values, cancellationToken);
    }

    public async Task<OAuth2TokenResponse> ClientCredentialsAsync(
        ReadableOAuth2Options options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.TokenUrl))
        {
            throw new ArgumentException("TokenUrl is required.", nameof(options));
        }

        var values = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials")
        };

        AddClientCredentials(values, options);
        AddCommonExtraParameters(values, options);

        return await SendTokenRequestAsync(options, options.TokenUrl, values, cancellationToken);
    }

    public async Task<OAuth2TokenResponse> PasswordCredentialsAsync(
        ReadableOAuth2Options options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.TokenUrl))
        {
            throw new ArgumentException("TokenUrl is required.", nameof(options));
        }

        var values = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("username", options.Username ?? string.Empty),
            new("password", options.Password ?? string.Empty)
        };

        AddClientCredentials(values, options);
        AddCommonExtraParameters(values, options);

        return await SendTokenRequestAsync(options, options.TokenUrl, values, cancellationToken);
    }

    public async Task<OAuth2TokenResponse> RefreshAsync(
        ReadableOAuth2Options options,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenUrl = string.IsNullOrWhiteSpace(options.RefreshTokenUrl)
            ? options.TokenUrl
            : options.RefreshTokenUrl;
        if (string.IsNullOrWhiteSpace(tokenUrl))
        {
            throw new ArgumentException("TokenUrl is required.", nameof(options));
        }

        var values = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken)
        };

        AddClientCredentials(values, options);
        AddCommonExtraParameters(values, options);

        return await SendTokenRequestAsync(options, tokenUrl, values, cancellationToken);
    }

    public Task<OAuth2TokenResponse> RequestAsync(
        ReadableOAuth2Options options,
        OAuth2TokenRequestContext? context = null,
        CancellationToken cancellationToken = default)
    {
        return options.GrantType switch
        {
            ReadableOAuth2GrantType.AuthorizationCode => ExchangeAuthorizationCodeAsync(
                options,
                context?.Code ?? throw new ArgumentException("Authorization code is required.", nameof(context)),
                context.RedirectUri ?? options.RedirectUri ?? throw new ArgumentException("RedirectUri is required.", nameof(context)),
                context.CodeVerifier,
                cancellationToken),
            ReadableOAuth2GrantType.ClientCredentials => ClientCredentialsAsync(options, cancellationToken),
            ReadableOAuth2GrantType.PasswordCredentials => PasswordCredentialsAsync(options, cancellationToken),
            ReadableOAuth2GrantType.RefreshToken => RefreshAsync(
                options,
                context?.RefreshToken ?? throw new ArgumentException("Refresh token is required.", nameof(context)),
                cancellationToken),
            _ => throw new NotSupportedException($"OAuth2 grant type '{options.GrantType}' does not use the token endpoint.")
        };
    }

    private async Task<OAuth2TokenResponse> SendTokenRequestAsync(
        ReadableOAuth2Options options,
        string tokenUrl,
        List<KeyValuePair<string, string>> values,
        CancellationToken cancellationToken)
    {
        if (options.ClientAuthentication == ReadableOAuth2ClientAuthentication.ClientSecretPost
            && !string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            values.Add(new KeyValuePair<string, string>("client_secret", options.ClientSecret));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(values)
        };

        if (options.ClientAuthentication == ReadableOAuth2ClientAuthentication.ClientSecretBasic
            && !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("OAuth2 token response was empty.");
    }

    private static void AddClientCredentials(List<KeyValuePair<string, string>> values, ReadableOAuth2Options options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientId))
        {
            values.Add(new KeyValuePair<string, string>("client_id", options.ClientId));
        }
    }

    private static void AddCommonExtraParameters(List<KeyValuePair<string, string>> values, ReadableOAuth2Options options)
    {
        if (options.Scopes.Count > 0)
        {
            values.Add(new KeyValuePair<string, string>("scope", string.Join(' ', options.Scopes)));
        }

        if (!string.IsNullOrWhiteSpace(options.Audience))
        {
            values.Add(new KeyValuePair<string, string>("audience", options.Audience));
        }

        if (!string.IsNullOrWhiteSpace(options.Resource))
        {
            values.Add(new KeyValuePair<string, string>("resource", options.Resource));
        }

        foreach (var parameter in options.ExtraParameters)
        {
            values.Add(new KeyValuePair<string, string>(parameter.Key, parameter.Value ?? string.Empty));
        }
    }
}

public sealed class OAuth2TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalValues { get; set; }
}

public sealed class OAuth2TokenRequestContext
{
    public string? Code { get; set; }

    public string? RedirectUri { get; set; }

    public string? CodeVerifier { get; set; }

    public string? RefreshToken { get; set; }
}
