using FluentHttp.Json;

namespace FluentHttpFactory;

public class FluentHttpFactory : IFluentHttpFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FluentHttpFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient Create(string name)
    {
        return _httpClientFactory.CreateClient(name);
    }

    public FluentJsonHttpClient CreateJson(string name)
    {
        return new FluentJsonHttpClient(Create(name));
    }

    public FluentFormHttpClient CreateForm(string name)
    {
        return new FluentFormHttpClient(Create(name));
    }
}
