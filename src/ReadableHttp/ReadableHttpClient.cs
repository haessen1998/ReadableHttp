using System.Text.Json;
using ReadableHttp.Core;
using ReadableHttp.Execution;

namespace ReadableHttp;

public static class ReadableHttpClient
{
    public static ReadableHttpRequestBuilder Request(string url)
    {
        return new ReadableHttpRequestBuilder().WithUrl(url);
    }
}

public sealed class ReadableHttpRequestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ReadableRequest _request = new();
    private readonly ReadableExecutionContext _context = new();
    private IReadableHttpExecutor _executor = new ReadableHttpExecutor();

    public ReadableHttpRequestBuilder WithExecutor(IReadableHttpExecutor executor)
    {
        _executor = executor;
        return this;
    }

    public ReadableHttpRequestBuilder WithBaseAddress(string baseAddress)
    {
        _context.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
        return this;
    }

    public ReadableHttpRequestBuilder WithTimeout(TimeSpan timeout)
    {
        _context.Timeout = timeout;
        return this;
    }

    public ReadableHttpRequestBuilder WithUrl(string url)
    {
        _request.Url = url;
        return this;
    }

    public ReadableHttpRequestBuilder WithMethod(string method)
    {
        _request.Method = method;
        return this;
    }

    public ReadableHttpRequestBuilder Get() => WithMethod("GET");

    public ReadableHttpRequestBuilder Post() => WithMethod("POST");

    public ReadableHttpRequestBuilder Put() => WithMethod("PUT");

    public ReadableHttpRequestBuilder Patch() => WithMethod("PATCH");

    public ReadableHttpRequestBuilder Delete() => WithMethod("DELETE");

    public ReadableHttpRequestBuilder WithHeader(string name, object? value)
    {
        _request.Headers.Add(new ReadableNameValue { Name = name, Value = Convert.ToString(value), Enabled = true });
        return this;
    }

    public ReadableHttpRequestBuilder WithQuery(string name, object? value)
    {
        _request.Query.Add(new ReadableNameValue { Name = name, Value = Convert.ToString(value), Enabled = true });
        return this;
    }

    public ReadableHttpRequestBuilder WithVariable(string name, object? value)
    {
        _context.Variables[name] = Convert.ToString(value);
        return this;
    }

    public ReadableHttpRequestBuilder WithBearerToken(string token)
    {
        _request.Auth = new ReadableAuth { Type = ReadableAuthType.Bearer, Token = token };
        return this;
    }

    public ReadableHttpRequestBuilder WithBasicAuth(string username, string password)
    {
        _request.Auth = new ReadableAuth { Type = ReadableAuthType.Basic, Username = username, Password = password };
        return this;
    }

    public ReadableHttpRequestBuilder WithApiKey(string name, string value, ReadableApiKeyLocation location = ReadableApiKeyLocation.Header)
    {
        _request.Auth = new ReadableAuth { Type = ReadableAuthType.ApiKey, Name = name, Value = value, ApiKeyLocation = location };
        return this;
    }

    public ReadableHttpRequestBuilder WithJsonBody<T>(T body)
    {
        _request.Body = new ReadableBody
        {
            Type = ReadableBodyType.Json,
            Content = JsonSerializer.Serialize(body, JsonOptions),
            ContentType = "application/json"
        };
        return this;
    }

    public ReadableHttpRequestBuilder WithRawBody(string content, string contentType = "text/plain")
    {
        _request.Body = new ReadableBody
        {
            Type = ReadableBodyType.Raw,
            Content = content,
            ContentType = contentType
        };
        return this;
    }

    public ReadableHttpRequestBuilder WithFormBody(params (string Name, object? Value)[] values)
    {
        _request.Body = new ReadableBody
        {
            Type = ReadableBodyType.FormUrlEncoded,
            Form = values.Select(value => new ReadableNameValue
            {
                Name = value.Name,
                Value = Convert.ToString(value.Value),
                Enabled = true
            }).ToList()
        };
        return this;
    }

    public Task<ReadableExchange> SendExchangeAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendExchangeAsync(_request, _context, cancellationToken);
    }

    public IAsyncEnumerable<ReadableStreamMessage> StreamAsync(
        ReadableStreamFormat format = ReadableStreamFormat.Auto,
        CancellationToken cancellationToken = default)
    {
        return _executor.StreamAsync(
            _request,
            _context,
            new ReadableStreamOptions { Format = format },
            cancellationToken);
    }

    public async Task<T> SendAsync<T>(CancellationToken cancellationToken = default)
    {
        var exchange = await SendExchangeAsync(cancellationToken);
        if (exchange.Error is not null)
        {
            throw new HttpRequestException(exchange.Error.Message);
        }

        var response = exchange.Response ?? throw new HttpRequestException("HTTP request did not return a response.");
        if (response.StatusCode is < 200 or >= 300)
        {
            throw new HttpRequestException(
                $"HTTP request failed with status {response.StatusCode} ({response.ReasonPhrase}). Response body: {response.BodyText}");
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)(response.BodyText ?? string.Empty);
        }

        return JsonSerializer.Deserialize<T>(response.BodyText ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("Response JSON content was empty or null.");
    }
}
