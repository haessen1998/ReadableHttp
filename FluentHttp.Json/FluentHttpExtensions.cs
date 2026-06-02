using System.Collections;
using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
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

    public static string Query(this string url, object values)
    {
        return url.Query(ToQueryValues(values));
    }

    public static string Query(this string url, IReadOnlyDictionary<string, object?> values)
    {
        return url.Query(values.SelectMany(pair => CreateQueryPairs(pair.Key, pair.Value)));
    }

    public static string Query(this string url, params (string Key, object? Value)[] values)
    {
        return url.Query(values.SelectMany(pair => CreateQueryPairs(pair.Key, pair.Value)));
    }

    private static string Query(this string url, IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var query = string.Join("&", values.Select(pair =>
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Query argument name cannot be null or empty.", nameof(values));
            }

            return $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";
        }));

        if (string.IsNullOrEmpty(query))
        {
            return url;
        }

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

    public static Task<TOut> GetFromJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        object query,
        CancellationToken cancellation = default)
    {
        return httpClient.GetFromJsonAsync<TOut>(url.Query(query), cancellation);
    }

    public static Task<TOut> GetFromJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        IReadOnlyDictionary<string, object?> query,
        CancellationToken cancellation = default)
    {
        return httpClient.GetFromJsonAsync<TOut>(url.Query(query), cancellation);
    }

    public static Task<TOut> GetFromJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        params (string Key, object? Value)[] query)
    {
        return httpClient.GetFromJsonAsync<TOut>(url.Query(query));
    }

    public static Task<TOut> PostFromJsonAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        TIn body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadJsonAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public static Task<TOut> PostFromJsonAsync<TOut>(
        this HttpClient httpClient,
        string url,
        object body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadJsonAsync<TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public static Task<TOut> PostFromFormAsync<TIn, TOut>(
        this HttpClient httpClient,
        string url,
        TIn body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadFormAsync<TIn, TOut>(url, HttpMethod.Post, body, cancellation);
    }

    public static Task<TOut> PostFromFormAsync<TOut>(
        this HttpClient httpClient,
        string url,
        object body,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadFormAsync<TOut>(url, HttpMethod.Post, body, cancellation);
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

    public static IAsyncEnumerable<TOut> PostStreamAsync<TOut>(
        this HttpClient httpClient,
        string url,
        object body,
        string streamType = EnumerableStream,
        CancellationToken cancellation = default)
    {
        return httpClient.ReadStreamAsync<TOut>(url, HttpMethod.Post, body, streamType, cancellation);
    }

    public static async Task SendJsonAsync(
        this HttpClient httpClient,
        string url,
        HttpMethod method,
        CancellationToken cancellation = default)
    {
        _ = await FluentHttpRequest.SendJsonAsync<string>(httpClient, url, method, null, cancellation);
    }

    private static IEnumerable<KeyValuePair<string, string>> ToQueryValues(object values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.SelectMany(pair => CreateQueryPairs(pair.Key, pair.Value));
        }

        if (values is IEnumerable<KeyValuePair<string, object?>> objectPairs)
        {
            return objectPairs.SelectMany(pair => CreateQueryPairs(pair.Key, pair.Value));
        }

        if (values is IEnumerable<KeyValuePair<string, string?>> stringPairs)
        {
            return stringPairs.SelectMany(pair => CreateQueryPairs(pair.Key, pair.Value));
        }

        if (values is IEnumerable and not string)
        {
            throw new ArgumentException("Query values must be an object or key-value collection.", nameof(values));
        }

        return values
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .SelectMany(property =>
            {
                var name = FluentHttpRequest.JsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                return CreateQueryPairs(name, property.GetValue(values));
            });
    }

    private static IEnumerable<KeyValuePair<string, string>> CreateQueryPairs(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Query argument name cannot be null or empty.", nameof(name));
        }

        if (value is IEnumerable values and not string)
        {
            foreach (var item in values)
            {
                yield return new KeyValuePair<string, string>(name, FormatQueryValue(item));
            }

            yield break;
        }

        yield return new KeyValuePair<string, string>(name, FormatQueryValue(value));
    }

    private static string FormatQueryValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean.ToString().ToLowerInvariant(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
