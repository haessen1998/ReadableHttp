namespace FluentHttp.Json;

public abstract class FluentHttpClientBase<TClient>
    where TClient : FluentHttpClientBase<TClient>
{
    protected FluentHttpClientBase(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public HttpClient HttpClient { get; }

    public TClient SetBaseUrl(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit)
    {
        HttpClient.SetBaseUrl(baseUrl, timeout, unit);
        return Self;
    }

    public TClient SetBaseUrl(string baseUrl, TimeSpan timeout)
    {
        HttpClient.SetBaseUrl(baseUrl, timeout);
        return Self;
    }

    public TClient AddAuthentication(string scheme, string? parameter = null)
    {
        HttpClient.AddAuthentication(scheme, parameter);
        return Self;
    }

    public TClient AddBasicAuthentication(string username, string password)
    {
        HttpClient.AddBasicAuthentication(username, password);
        return Self;
    }

    public TClient AddBearerAuthentication(string token)
    {
        HttpClient.AddBearerAuthentication(token);
        return Self;
    }

    public TClient AddHeaders(params (object, object?)[] headers)
    {
        HttpClient.AddHeaders(headers);
        return Self;
    }

    private TClient Self => (TClient)this;
}
