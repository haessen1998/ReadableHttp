namespace FluentHttp.Json;

public sealed class FluentJsonHttpClient : FluentHttpClientBase<FluentJsonHttpClient>
{
    public FluentJsonHttpClient(HttpClient httpClient)
        : base(httpClient)
    {
    }

    public Task<TOut> GetAsync<TOut>(string url, CancellationToken cancellation = default)
    {
        return SendAsync<TOut>(url, HttpMethod.Get, cancellation);
    }

    public Task<TOut> PostAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public Task<TOut> PutAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Put, body, cancellation);
    }

    public Task<TOut> PatchAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellation = default)
    {
        return SendAsync<TIn, TOut>(url, HttpMethod.Patch, body, cancellation);
    }

    public Task<TOut> DeleteAsync<TOut>(string url, CancellationToken cancellation = default)
    {
        return SendAsync<TOut>(url, HttpMethod.Delete, cancellation);
    }

    public Task<TOut> SendAsync<TOut>(string url, HttpMethod method, CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(HttpClient, url, method, null, cancellation);
    }

    public Task<TOut> SendAsync<TIn, TOut>(string url, HttpMethod method, TIn body, CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            HttpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            cancellation);
    }

    public IAsyncEnumerable<TOut> StreamAsync<TIn, TOut>(
        string url,
        HttpMethod method,
        TIn body,
        string streamType = FluentHttpExtensions.EnumerableStream,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendStreamAsync<TOut>(
            HttpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            streamType,
            cancellation);
    }

    public IAsyncEnumerable<TOut> PostStreamAsync<TIn, TOut>(
        string url,
        TIn body,
        string streamType = FluentHttpExtensions.EnumerableStream,
        CancellationToken cancellation = default)
    {
        return StreamAsync<TIn, TOut>(url, HttpMethod.Post, body, streamType, cancellation);
    }
}
