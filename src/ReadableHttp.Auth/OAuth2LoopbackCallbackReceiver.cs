using System.Net;
using System.Text;

namespace ReadableHttp.Auth;

public sealed class OAuth2LoopbackCallbackReceiver
{
    public async Task<OAuth2CallbackResult> ReceiveAsync(
        Uri redirectUri,
        string responseHtml = "<html><body>You can close this window.</body></html>",
        CancellationToken cancellationToken = default)
    {
        if (!redirectUri.IsLoopback)
        {
            throw new ArgumentException("Redirect URI must be loopback.", nameof(redirectUri));
        }

        using var listener = new HttpListener();
        var prefix = BuildPrefix(redirectUri);
        listener.Prefixes.Add(prefix);
        listener.Start();

        await using var registration = cancellationToken.Register(static state =>
        {
            try
            {
                ((HttpListener)state!).Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        }, listener);

        var context = await listener.GetContextAsync();
        var request = context.Request;
        var responseBytes = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = responseBytes.Length;
        await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
        context.Response.Close();

        return new OAuth2CallbackResult
        {
            Code = request.QueryString["code"],
            State = request.QueryString["state"],
            Error = request.QueryString["error"],
            ErrorDescription = request.QueryString["error_description"]
        };
    }

    private static string BuildPrefix(Uri redirectUri)
    {
        var path = redirectUri.AbsolutePath;
        if (!path.EndsWith('/'))
        {
            path += "/";
        }

        return $"{redirectUri.Scheme}://{redirectUri.Host}:{redirectUri.Port}{path}";
    }
}

public sealed class OAuth2CallbackResult
{
    public string? Code { get; init; }

    public string? State { get; init; }

    public string? Error { get; init; }

    public string? ErrorDescription { get; init; }
}
