using System.Web;
using ReadableHttp.Core;

namespace ReadableHttp.Auth;

public sealed class OAuth2AuthorizationRequest
{
    public required string AuthorizationUrl { get; init; }

    public required string ClientId { get; init; }

    public required string RedirectUri { get; init; }

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public string? State { get; init; }

    public OAuth2PkcePair? Pkce { get; init; }

    public Uri ToUri()
    {
        var builder = new UriBuilder(AuthorizationUrl);
        var query = HttpUtility.ParseQueryString(builder.Query);
        query["response_type"] = "code";
        query["client_id"] = ClientId;
        query["redirect_uri"] = RedirectUri;

        if (Scopes.Count > 0)
        {
            query["scope"] = string.Join(' ', Scopes);
        }

        if (!string.IsNullOrWhiteSpace(State))
        {
            query["state"] = State;
        }

        if (Pkce is not null)
        {
            query["code_challenge"] = Pkce.CodeChallenge;
            query["code_challenge_method"] = Pkce.Method;
        }

        builder.Query = query.ToString();
        return builder.Uri;
    }

    public static OAuth2AuthorizationRequest FromOptions(ReadableOAuth2Options options, string redirectUri, string? state = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.AuthorizationUrl))
        {
            throw new ArgumentException("AuthorizationUrl is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("ClientId is required.", nameof(options));
        }

        return new OAuth2AuthorizationRequest
        {
            AuthorizationUrl = options.AuthorizationUrl,
            ClientId = options.ClientId,
            RedirectUri = redirectUri,
            Scopes = options.Scopes,
            State = state ?? options.State,
            Pkce = options.UsePkce ? OAuth2Pkce.Create() : null
        };
    }
}
