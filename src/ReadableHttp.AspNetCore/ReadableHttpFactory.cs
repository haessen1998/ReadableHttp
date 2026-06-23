using ReadableHttp.Execution;

namespace ReadableHttp.AspNetCore;

public sealed class ReadableHttpFactory : IReadableHttpFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ReadableHttpFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateClient()
    {
        return CreateClient(ReadableHttpClientNames.Default);
    }

    public HttpClient CreateClient(string name)
    {
        return _httpClientFactory.CreateClient(name);
    }

    public IReadableHttpExecutor CreateExecutor()
    {
        return CreateExecutor(ReadableHttpClientNames.Default);
    }

    public IReadableHttpExecutor CreateExecutor(string name)
    {
        return new ReadableHttpExecutor(() => _httpClientFactory.CreateClient(name));
    }
}
