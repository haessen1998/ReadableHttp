using System.Net;

namespace ReadableHttp.Tests;

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        _sendAsync = sendAsync;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await CloneRequestAsync(request, cancellationToken));
        return await _sendAsync(request, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
