using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace FluentHttp.Json;

public static class FluentHttpExtensions
{
    public const string BearerScheme = "Bearer";
    public const string BasicScheme = "Basic";

    public const string MicrosecondsUnit = "Microseconds";
    public const string SecondsUnit = "Seconds";
    public const string MinutesUnit = "Minutes";
    public const string HoursUnit = "Hours";

    public const string EnumerableStream = "Enumerable";
    public const string EventStream = "Event";
    public const string FileStream = "File";

    public static HttpClient SetBaseUrl(this HttpClient httpClient, string baseUrl, int timeout = 60, string unit = SecondsUnit)
    {
        return httpClient.SetBaseUrl(new Uri(baseUrl, UriKind.Absolute), ToTimeout(timeout, unit));
    }

    public static HttpClient SetBaseUrl(this HttpClient httpClient, string baseUrl, TimeSpan timeout)
    {
        return httpClient.SetBaseUrl(new Uri(baseUrl, UriKind.Absolute), timeout);
    }

    public static HttpClient SetBaseUrl(this HttpClient httpClient, Uri baseUrl, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUrl);

        if (!baseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Base URL must be an absolute URI.", nameof(baseUrl));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        httpClient.BaseAddress = baseUrl;
        httpClient.Timeout = timeout;
        return httpClient;
    }

    public static TimeSpan ToTimeout(int timeout, string unit = SecondsUnit)
    {
        if (timeout <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        return unit switch
        {
            MicrosecondsUnit => TimeSpan.FromMicroseconds(timeout),
            SecondsUnit => TimeSpan.FromSeconds(timeout),
            MinutesUnit => TimeSpan.FromMinutes(timeout),
            HoursUnit => TimeSpan.FromHours(timeout),
            _ => TimeSpan.FromSeconds(timeout)
        };
    }

    public static HttpClient AddAuthentication(this HttpClient httpClient, string scheme, string? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("Authentication scheme cannot be null or empty.", nameof(scheme));
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, parameter);
        return httpClient;
    }

    public static HttpClient AddBasicAuthentication(this HttpClient httpClient, string username, string password)
    {
        var parameter = $"{username}:{password}";
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(parameter));

        return httpClient.AddAuthentication(BasicScheme, value);
    }

    public static HttpClient AddBearerAuthentication(this HttpClient httpClient, string token)
    {
        return httpClient.AddAuthentication(BearerScheme, token);
    }

    public static HttpClient AddHeaders(this HttpClient httpClient, params (object, object?)[] headers)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        foreach (var (name, value) in headers)
        {
            var headerName = name?.ToString();
            if (string.IsNullOrWhiteSpace(headerName))
            {
                throw new ArgumentException("Header name cannot be null or empty.", nameof(headers));
            }

            var headerValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!httpClient.DefaultRequestHeaders.TryAddWithoutValidation(headerName, headerValue))
            {
                throw new InvalidOperationException($"Header '{headerName}' cannot be added to DefaultRequestHeaders.");
            }
        }

        return httpClient;
    }

    public static string AppendUrl(this string url, params (object, object?)[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (arguments.Length == 0)
        {
            return url;
        }

        var query = string.Join("&", arguments.Select(argument =>
        {
            var name = argument.Item1?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Query argument name cannot be null or empty.", nameof(arguments));
            }

            return $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(Convert.ToString(argument.Item2, CultureInfo.InvariantCulture) ?? string.Empty)}";
        }));

        var separator = url.Contains('?', StringComparison.Ordinal)
            ? url.EndsWith('?') || url.EndsWith('&') ? string.Empty : "&"
            : "?";

        return $"{url}{separator}{query}";
    }

    public static Task<TOut> GetFromJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadJsonAsync<TOut>(url, HttpMethod.Get, cancellation);
    }

    public static Task<TOut> PostFromJsonAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        TIn body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadJsonAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public static Task<TOut> PostFromFormAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        TIn body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadFormAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public static Task<TOut> ReadJsonAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        TIn body,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            cancellation);
    }

    public static Task<TOut> ReadJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        object body,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            cancellation);
    }

    public static Task<TOut> ReadJsonAsync<TOut>(
       this HttpClient httpClient,
       string url,
       HttpMethod method,
       CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(httpClient, url, method, null, cancellation);
    }

    public static Task<TOut> ReadFormAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        TIn body,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateFormContent(body),
            cancellation);
    }

    public static Task<TOut> ReadFormAsync<TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        object body,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendJsonAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateFormContent(body),
            cancellation);
    }

    public static IAsyncEnumerable<TOut> ReadStreamAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        TIn body,
        string streamType = EnumerableStream,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendStreamAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            streamType,
            cancellation);
    }

    public static IAsyncEnumerable<TOut> ReadStreamAsync<TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        object body,
        string streamType = EnumerableStream,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendStreamAsync<TOut>(
            httpClient,
            url,
            method,
            FluentHttpRequest.CreateJsonContent(body),
            streamType,
            cancellation);
    }

    public static IAsyncEnumerable<TOut> ReadStreamAsync<TOut>(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        string streamType = EnumerableStream,
        CancellationToken cancellation = default)
    {
        return FluentHttpRequest.SendStreamAsync<TOut>(httpClient, url, method, null, streamType, cancellation);
    }

    public static IAsyncEnumerable<TOut> PostStreamAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        TIn body,
        string streamType = EnumerableStream,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadStreamAsync<TIn, TOut>(url, HttpMethod.Post, body, streamType, cancellation);
    }

    public static async Task SendJsonAsync(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        CancellationToken cancellation = default)
    {
        _ = await FluentHttpRequest.SendJsonAsync<string>(httpClient, url, method, null, cancellation);
    }
}
