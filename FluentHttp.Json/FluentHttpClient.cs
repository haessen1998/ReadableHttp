using System.Net;

namespace FluentHttp.Json;

public class FluentHttpClient : HttpClient
{
    public static FluentJsonHttpClient CreateJson()
    {
        return new FluentJsonHttpClient(new HttpClient());
    }

    public static FluentJsonHttpClient CreateJson(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit)
    {
        return CreateJson().SetBaseUrl(baseUrl, timeout, unit);
    }

    public static FluentJsonHttpClient CreateJson(string baseUrl, TimeSpan timeout)
    {
        return CreateJson().SetBaseUrl(baseUrl, timeout);
    }

    public static FluentFormHttpClient CreateForm()
    {
        return new FluentFormHttpClient(new HttpClient());
    }

    public static FluentFormHttpClient CreateForm(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit)
    {
        return CreateForm().SetBaseUrl(baseUrl, timeout, unit);
    }

    public static FluentFormHttpClient CreateForm(string baseUrl, TimeSpan timeout)
    {
        return CreateForm().SetBaseUrl(baseUrl, timeout);
    }

    public static HttpClient Create()
    {
        return new HttpClient();
    }

    public static HttpClient Create(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit)
    {
        return Create().SetBaseUrl(baseUrl, timeout, unit);
    }

    public static HttpClient Create(string baseUrl, TimeSpan timeout)
    {
        return Create().SetBaseUrl(baseUrl, timeout);
    }

    public static HttpClient CreateWithCookie(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit, params (object, object)[] cookies)
    {
        return CreateWithCookie(baseUrl, FluentHttpExtensions.ToTimeout(timeout, unit), cookies);
    }

    public static HttpClient CreateWithCookie(string baseUrl, TimeSpan timeout, params (object, object)[] cookies)
    {
        return CreateHttpClientWithCookie(baseUrl, timeout, cookies);
    }

    public static FluentJsonHttpClient CreateJsonWithCookie(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit, params (object, object)[] cookies)
    {
        return CreateJsonWithCookie(baseUrl, FluentHttpExtensions.ToTimeout(timeout, unit), cookies);
    }

    public static FluentJsonHttpClient CreateJsonWithCookie(string baseUrl, TimeSpan timeout, params (object, object)[] cookies)
    {
        return new FluentJsonHttpClient(CreateHttpClientWithCookie(baseUrl, timeout, cookies));
    }

    public static FluentFormHttpClient CreateFormWithCookie(string baseUrl, int timeout = 60, string unit = FluentHttpExtensions.SecondsUnit, params (object, object)[] cookies)
    {
        return CreateFormWithCookie(baseUrl, FluentHttpExtensions.ToTimeout(timeout, unit), cookies);
    }

    public static FluentFormHttpClient CreateFormWithCookie(string baseUrl, TimeSpan timeout, params (object, object)[] cookies)
    {
        return new FluentFormHttpClient(CreateHttpClientWithCookie(baseUrl, timeout, cookies));
    }

    private static HttpClient CreateHttpClientWithCookie(string baseUrl, TimeSpan timeout, params (object, object)[] cookies)
    {
        var uri = new Uri(baseUrl, UriKind.Absolute);
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };

        foreach (var (name, value) in cookies)
        {
            var cookieName = name?.ToString();
            if (string.IsNullOrWhiteSpace(cookieName))
            {
                throw new ArgumentException("Cookie name cannot be null or empty.", nameof(cookies));
            }

            handler.CookieContainer.Add(uri, new Cookie(cookieName, value?.ToString() ?? string.Empty));
        }

        return new HttpClient(handler).SetBaseUrl(uri, timeout);
    }
}
