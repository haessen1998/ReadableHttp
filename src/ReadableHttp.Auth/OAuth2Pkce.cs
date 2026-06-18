using System.Security.Cryptography;
using System.Text;

namespace ReadableHttp.Auth;

public sealed record OAuth2PkcePair(string CodeVerifier, string CodeChallenge, string Method);

public static class OAuth2Pkce
{
    public static OAuth2PkcePair Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64UrlEncode(bytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return new OAuth2PkcePair(verifier, challenge, "S256");
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
