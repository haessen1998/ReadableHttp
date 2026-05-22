using System.Collections;
using System.Globalization;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FluentHttp.Json;

internal static class FluentHttpRequest
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal static async Task<TOut> SendJsonAsync<TOut>(
        HttpClient httpClient,
        string url,
        HttpMethod method,
        HttpContent? content,
        CancellationToken cancellation)
    {
        using var response = await SendAsync(httpClient, url, method, content, HttpCompletionOption.ResponseContentRead, cancellation);
        return await ReadResponseAsync<TOut>(response, cancellation);
    }

    internal static async IAsyncEnumerable<TOut> SendStreamAsync<TOut>(
        HttpClient httpClient,
        string url,
        HttpMethod method,
        HttpContent? content,
        string streamType,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        using var response = await SendAsync(httpClient, url, method, content, HttpCompletionOption.ResponseHeadersRead, cancellation);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);

        if (streamType == FluentHttpExtensions.EnumerableStream)
        {
            await foreach (var message in JsonSerializer.DeserializeAsyncEnumerable<TOut>(
                stream,
                JsonOptions,
                cancellationToken: cancellation))
            {
                yield return RequireValue(message, "Stream JSON item was null.");
            }

            yield break;
        }

        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellation)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (typeof(TOut) == typeof(string))
            {
                yield return (TOut)(object)line;
                continue;
            }

            yield return RequireValue(JsonSerializer.Deserialize<TOut>(line, JsonOptions), "Stream line could not be deserialized.");
        }
    }

    internal static HttpContent CreateJsonContent<T>(T arguments)
    {
        return JsonContent.Create(arguments, options: JsonOptions);
    }

    internal static HttpContent CreateFormContent<T>(T arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return new FormUrlEncodedContent(ToFormValues(arguments));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        string url,
        HttpMethod method,
        HttpContent? content,
        HttpCompletionOption completionOption,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(method);

        using var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };

        var response = await httpClient.SendAsync(request, completionOption, cancellation);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var errorMessage = await response.Content.ReadAsStringAsync(cancellation);
        var statusCode = response.StatusCode;
        var reasonPhrase = response.ReasonPhrase;
        response.Dispose();

        throw new HttpRequestException(
            $"HTTP request failed with status {(int)statusCode} ({reasonPhrase}). Response body: {errorMessage}",
            null,
            statusCode);
    }

    private static async Task<TOut> ReadResponseAsync<TOut>(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (typeof(TOut) == typeof(string))
        {
            var text = await response.Content.ReadAsStringAsync(cancellation);
            return (TOut)(object)text;
        }

        var result = await response.Content.ReadFromJsonAsync<TOut>(JsonOptions, cancellation);
        return RequireValue(result, "Response JSON content was empty or null.");
    }

    private static IEnumerable<KeyValuePair<string, string>> ToFormValues<T>(T arguments)
    {
        if (arguments is IEnumerable<KeyValuePair<string, string?>> stringValues)
        {
            return stringValues.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value ?? string.Empty));
        }

        if (arguments is IEnumerable<KeyValuePair<string, object?>> objectValues)
        {
            return objectValues.Select(pair => new KeyValuePair<string, string>(pair.Key, FormatFormValue(pair.Value)));
        }

        if (arguments is IEnumerable and not string)
        {
            throw new ArgumentException("Form body must be an object or key-value collection.", nameof(arguments));
        }

        return arguments!
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .Select(property =>
            {
                var name = FluentHttpRequest.JsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                var value = property.GetValue(arguments);
                return new KeyValuePair<string, string>(name, FormatFormValue(value));
            });
    }

    private static string FormatFormValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value, JsonOptions)
        };
    }

    private static T RequireValue<T>(T? value, string message)
    {
        return value ?? throw new InvalidOperationException(message);
    }
}
