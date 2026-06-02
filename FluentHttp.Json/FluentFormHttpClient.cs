namespace FluentHttp.Json;

public sealed class FluentFormHttpClient : FluentHttpClientBase<FluentFormHttpClient>
{
    public FluentFormHttpClient(HttpClient httpClient)
        : base(httpClient)
    {
    }

    public Task<TOut> PostAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public Task<TOut> PostAsync<TOut>(string url, object body, CancellationToken cancellation = default)
    {
        return SendAsync<TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public Task<TOut> PutAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Put, body, cancellation);
    }

    public Task<TOut> PutAsync<TOut>(string url, object body, CancellationToken cancellation = default)
    {
        return SendAsync<TOut>(url, HttpMethod.Put, body, cancellation);
    }

    public Task<TOut> PatchAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Patch, body, cancellation);
    }

    public Task<TOut> PatchAsync<TOut>(string url, object body, CancellationToken cancellation = default)
    {
        return SendAsync<TOut>(url, HttpMethod.Patch, body, cancellation);
    }

    public Task<TOut> SendAsync<TIn, TOut>(string url, HttpMethod method, TIn body, CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            HttpClient,
            url,
            method,
            FluentHttpRequest.CreateFormContent(body),
            cancellation);
    }

    public Task<TOut> SendAsync<TOut>(string url, HttpMethod method, object body, CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            HttpClient,
            url,
            method,
            FluentHttpRequest.CreateFormContent(body),
            cancellation);
    }
}
