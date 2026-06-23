using ReadableHttp.Core;

namespace ReadableHttp.Auth;

public sealed class OAuth2TokenCache
{
    private readonly Dictionary<string, OAuth2CachedToken> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public OAuth2CachedToken? Get(string tokenId)
    {
        return _tokens.TryGetValue(tokenId, out var token) ? token : null;
    }

    public OAuth2CachedToken? GetUsable(string tokenId, int refreshBeforeExpirySeconds = 60)
    {
        var token = Get(tokenId);
        return token is null || token.ShouldRefresh(refreshBeforeExpirySeconds) ? null : token;
    }

    public OAuth2CachedToken Set(string tokenId, OAuth2TokenResponse response, DateTimeOffset? issuedAt = null)
    {
        issuedAt ??= DateTimeOffset.UtcNow;
        var token = OAuth2CachedToken.FromResponse(tokenId, response, issuedAt.Value);
        _tokens[tokenId] = token;
        return token;
    }

    public bool Remove(string tokenId)
    {
        return _tokens.Remove(tokenId);
    }

    public void Clear()
    {
        _tokens.Clear();
    }
}

public sealed class OAuth2CachedToken
{
    public string TokenId { get; set; } = string.Empty;

    public string? AccessToken { get; set; }

    public string? IdToken { get; set; }

    public string? TokenType { get; set; }

    public string? RefreshToken { get; set; }

    public string? Scope { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsExpired(DateTimeOffset? now = null)
    {
        return ExpiresAt is not null && ExpiresAt <= (now ?? DateTimeOffset.UtcNow);
    }

    public bool ShouldRefresh(int refreshBeforeExpirySeconds, DateTimeOffset? now = null)
    {
        if (ExpiresAt is null)
        {
            return false;
        }

        var threshold = TimeSpan.FromSeconds(Math.Max(0, refreshBeforeExpirySeconds));
        return ExpiresAt <= (now ?? DateTimeOffset.UtcNow).Add(threshold);
    }

    public string? GetToken(ReadableOAuth2TokenSource source)
    {
        return source == ReadableOAuth2TokenSource.IdToken ? IdToken : AccessToken;
    }

    public static OAuth2CachedToken FromResponse(string tokenId, OAuth2TokenResponse response, DateTimeOffset issuedAt)
    {
        return new OAuth2CachedToken
        {
            TokenId = tokenId,
            AccessToken = response.AccessToken,
            IdToken = response.IdToken,
            TokenType = response.TokenType,
            RefreshToken = response.RefreshToken,
            Scope = response.Scope,
            IssuedAt = issuedAt,
            ExpiresAt = response.ExpiresIn is { } expiresIn
                ? issuedAt.AddSeconds(expiresIn)
                : null
        };
    }
}
