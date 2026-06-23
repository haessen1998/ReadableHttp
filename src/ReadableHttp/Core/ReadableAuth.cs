namespace ReadableHttp.Core;

public enum ReadableAuthType
{
    None,
    Inherit,
    Basic,
    Bearer,
    ApiKey,
    OAuth1,
    OAuth2
}

public enum ReadableApiKeyLocation
{
    Header,
    Query
}

public enum ReadableTokenPlacement
{
    Header,
    Query,
    RequestBody
}

public enum ReadableOAuth2GrantType
{
    AuthorizationCode,
    ClientCredentials,
    PasswordCredentials,
    Implicit,
    RefreshToken
}

public enum ReadableOAuth2ClientAuthentication
{
    None,
    ClientSecretBasic,
    ClientSecretPost
}

public enum ReadableOAuth2TokenSource
{
    AccessToken,
    IdToken
}

public enum ReadableOAuth1SignatureMethod
{
    HmacSha1,
    PlainText,
    RsaSha1
}

public sealed class ReadableAuth
{
    public ReadableAuthType Type { get; set; } = ReadableAuthType.None;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Token { get; set; }

    public string? Name { get; set; }

    public string? Value { get; set; }

    public ReadableApiKeyLocation ApiKeyLocation { get; set; } = ReadableApiKeyLocation.Header;

    public ReadableOAuth1Options? OAuth1 { get; set; }

    public ReadableOAuth2Options? OAuth2 { get; set; }
}

public sealed class ReadableOAuth2Options
{
    public ReadableOAuth2GrantType GrantType { get; set; } = ReadableOAuth2GrantType.AuthorizationCode;

    public string? AuthorizationUrl { get; set; }

    public string? TokenUrl { get; set; }

    public string? RefreshTokenUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public ReadableOAuth2ClientAuthentication ClientAuthentication { get; set; } = ReadableOAuth2ClientAuthentication.ClientSecretBasic;

    public string? RedirectUri { get; set; }

    public bool UsePkce { get; set; } = true;

    public List<string> Scopes { get; set; } = [];

    public string? State { get; set; }

    public string? Audience { get; set; }

    public string? Resource { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public Dictionary<string, string?> ExtraParameters { get; set; } = [];

    public ReadableOAuth2TokenSource TokenSource { get; set; } = ReadableOAuth2TokenSource.AccessToken;

    public string TokenId { get; set; } = "credentials";

    public ReadableTokenPlacement TokenPlacement { get; set; } = ReadableTokenPlacement.Header;

    public string TokenName { get; set; } = "Authorization";

    public string HeaderPrefix { get; set; } = "Bearer";

    public bool AutoFetchToken { get; set; }

    public bool AutoRefreshToken { get; set; }

    public int RefreshBeforeExpirySeconds { get; set; } = 60;
}

public sealed class ReadableOAuth1Options
{
    public string? ConsumerKey { get; set; }

    public string? ConsumerSecret { get; set; }

    public string? Token { get; set; }

    public string? TokenSecret { get; set; }

    public ReadableOAuth1SignatureMethod SignatureMethod { get; set; } = ReadableOAuth1SignatureMethod.HmacSha1;

    public ReadableTokenPlacement Placement { get; set; } = ReadableTokenPlacement.Header;

    public string Version { get; set; } = "1.0";
}
