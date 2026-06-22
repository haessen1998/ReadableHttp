using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadableHttp.Core;

namespace ReadableHttp.Execution;

public sealed class ReadableHttpExecutor : IReadableHttpExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private readonly HttpMessageHandler? _handler;
    private readonly Func<HttpClient>? _httpClientFactory;

    public ReadableHttpExecutor()
    {
    }

    public ReadableHttpExecutor(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public ReadableHttpExecutor(Func<HttpClient> httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ReadableExchange> SendAsync(
        ReadableRequest request,
        ReadableExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        return await SendExchangeAsync(request, context, cancellationToken);
    }

    public async Task<ReadableExchange> SendExchangeAsync(
        ReadableRequest request,
        ReadableExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        context ??= new ReadableExecutionContext();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var timings = new List<ReadableExchangeTiming>();
        var buildStarted = stopwatch.Elapsed;
        var exchange = new ReadableExchange
        {
            Request = ReadableRequestVariableResolver.Resolve(request, context),
            StartedAt = startedAt,
            Timings = timings
        };
        ApplyRequestOptions(exchange.Request, context);
        AddTiming(timings, "Build Request", buildStarted, stopwatch.Elapsed);

        try
        {
            var clientStarted = stopwatch.Elapsed;
            using var httpClient = CreateHttpClient(context);
            AddTiming(timings, "Create HttpClient", clientStarted, stopwatch.Elapsed);

            var sendStarted = stopwatch.Elapsed;
            var result = await SendWithRedirectsAsync(httpClient, exchange.Request, context, cancellationToken);
            AddTiming(timings, "Send HTTP", sendStarted, stopwatch.Elapsed);
            exchange.RawRequestPreview = result.RawRequestPreview;
            using var response = result.Response;

            var readStarted = stopwatch.Elapsed;
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            AddTiming(timings, "Read Response", readStarted, stopwatch.Elapsed);
            stopwatch.Stop();

            exchange.Response = new ReadableResponse
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Headers = ReadHeaders(response),
                Cookies = ReadCookies(response),
                Redirects = result.Redirects,
                BodyBytes = bytes,
                BodyText = TryReadText(bytes, response.Content.Headers.ContentType),
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Duration = stopwatch.Elapsed,
                Size = bytes.LongLength
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            exchange.Error = new ReadableExecutionError
            {
                Type = exception.GetType().Name,
                Message = exception.Message
            };
        }
        finally
        {
            exchange.FinishedAt = DateTimeOffset.UtcNow;
        }

        return exchange;
    }

    private static void AddTiming(
        List<ReadableExchangeTiming> timings,
        string name,
        TimeSpan startOffset,
        TimeSpan finishedAt)
    {
        timings.Add(new ReadableExchangeTiming
        {
            Name = name,
            StartOffset = startOffset,
            Duration = finishedAt - startOffset
        });
    }

    public async IAsyncEnumerable<ReadableStreamMessage> StreamAsync(
        ReadableRequest request,
        ReadableExecutionContext? context = null,
        ReadableStreamOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        context ??= new ReadableExecutionContext();
        options ??= new ReadableStreamOptions();

        using var httpClient = CreateHttpClient(context);
        using var httpRequest = ReadableHttpRequestMessageFactory.Create(
            ReadableRequestVariableResolver.Resolve(request, context),
            context);

        HttpResponseMessage? response = null;
        try
        {
            response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            yield return new ReadableStreamMessage
            {
                Type = ReadableStreamMessageType.Headers,
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Headers = ReadHeaders(response)
            };

            var format = ResolveStreamFormat(options.Format, response.Content.Headers.ContentType);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            if (format == ReadableStreamFormat.Raw)
            {
                await foreach (var message in ReadRawStreamAsync(stream, options.BufferSize, cancellationToken))
                {
                    yield return message;
                }
            }
            else if (format == ReadableStreamFormat.ServerSentEvents)
            {
                await foreach (var message in ReadServerSentEventsAsync(stream, cancellationToken))
                {
                    yield return message;
                }
            }
            else
            {
                await foreach (var message in ReadLinesAsync(stream, cancellationToken))
                {
                    yield return message;
                }
            }

            yield return new ReadableStreamMessage
            {
                Type = ReadableStreamMessageType.Completed
            };
        }
        finally
        {
            response?.Dispose();
        }
    }

    private HttpClient CreateHttpClient(ReadableExecutionContext context)
    {
        if (_httpClientFactory is not null)
        {
            var client = _httpClientFactory();
            if (context.BaseAddress is not null)
            {
                client.BaseAddress = context.BaseAddress;
            }

            client.Timeout = context.Timeout;
            return client;
        }

        var handler = _handler ?? CreateHandler(context);

        var httpClient = new HttpClient(handler, disposeHandler: _handler is null)
        {
            BaseAddress = context.BaseAddress,
            Timeout = context.Timeout
        };

        return httpClient;
    }

    private static HttpClientHandler CreateHandler(ReadableExecutionContext context)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = context.UseCookies,
            CookieContainer = new CookieContainer()
        };

        ApplyProxy(handler, context.Proxy);

        if (context.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        foreach (var cookie in context.Cookies)
        {
            if (!string.IsNullOrWhiteSpace(cookie.Domain))
            {
                handler.CookieContainer.Add(new Cookie(cookie.Name, cookie.Value ?? string.Empty, cookie.Path ?? "/", cookie.Domain));
            }
        }

        return handler;
    }

    private static async Task<ReadableSendResult> SendWithRedirectsAsync(
        HttpClient httpClient,
        ReadableRequest request,
        ReadableExecutionContext context,
        CancellationToken cancellationToken)
    {
        var redirects = new List<ReadableRedirect>();
        var currentUrl = ReadableHttpRequestMessageFactory.BuildUrl(request);
        var currentMethod = request.Method;
        var includeBody = true;
        string? rawRequestPreview = null;

        for (var redirectCount = 0; redirectCount < 10; redirectCount++)
        {
            var hopRequest = CloneForHop(request, currentUrl, currentMethod, includeBody);
            using var httpRequest = ReadableHttpRequestMessageFactory.Create(hopRequest, context);
            rawRequestPreview ??= await CreateRawRequestPreviewAsync(httpRequest, cancellationToken);

            var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            if (!context.FollowRedirects || !IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                return new ReadableSendResult(response, redirects, rawRequestPreview ?? string.Empty);
            }

            var nextUrl = ResolveRedirectUrl(currentUrl, response.Headers.Location);
            redirects.Add(new ReadableRedirect
            {
                StatusCode = (int)response.StatusCode,
                Location = nextUrl
            });

            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther)
            {
                currentMethod = "GET";
                includeBody = false;
            }

            currentUrl = nextUrl;
            response.Dispose();
        }

        throw new InvalidOperationException("Too many HTTP redirects.");
    }

    private static ReadableRequest CloneForHop(ReadableRequest request, string url, string method, bool includeBody)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var clone = JsonSerializer.Deserialize<ReadableRequest>(json, JsonOptions) ?? new ReadableRequest();
        clone.Url = url;
        clone.Query.Clear();
        clone.PathParameters.Clear();
        clone.Method = method;
        if (!includeBody)
        {
            clone.Body = null;
        }

        return clone;
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static string ResolveRedirectUrl(string currentUrl, Uri location)
    {
        if (location.IsAbsoluteUri)
        {
            return location.ToString();
        }

        return new Uri(new Uri(currentUrl, UriKind.Absolute), location).ToString();
    }

    private static void ApplyProxy(HttpClientHandler handler, ReadableProxyOptions? proxy)
    {
        if (proxy is null || proxy.Mode == ReadableProxyMode.System)
        {
            handler.UseProxy = true;
            return;
        }

        if (proxy.Mode == ReadableProxyMode.None)
        {
            handler.UseProxy = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(proxy.Url))
        {
            throw new InvalidOperationException("Custom proxy requires Url.");
        }

        var webProxy = new WebProxy(proxy.Url);
        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
        }

        handler.Proxy = webProxy;
        handler.UseProxy = true;
    }

    private static void ApplyRequestOptions(ReadableRequest request, ReadableExecutionContext context)
    {
        if (request.Options.Timeout is { } timeout)
        {
            context.Timeout = timeout;
        }

        if (request.Options.FollowRedirects is { } followRedirects)
        {
            context.FollowRedirects = followRedirects;
        }

        if (request.Options.UseCookies is { } useCookies)
        {
            context.UseCookies = useCookies;
        }

        if (request.Options.IgnoreSslErrors is { } ignoreSslErrors)
        {
            context.IgnoreSslErrors = ignoreSslErrors;
        }

        if (request.Options.Proxy is not null)
        {
            context.Proxy = request.Options.Proxy;
        }
    }

    private static List<ReadableNameValue> ReadHeaders(HttpResponseMessage response)
    {
        return response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(header => header.Value.Select(value => new ReadableNameValue
            {
                Name = header.Key,
                Value = value,
                Enabled = true
            }))
            .ToList();
    }

    private static List<ReadableCookie> ReadCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return [];
        }

        return values.Select(value =>
        {
            var segments = value.Split(';', StringSplitOptions.TrimEntries);
            var first = segments[0];
            var pair = first.Split('=', 2);
            var cookie = new ReadableCookie
            {
                Name = pair[0],
                Value = pair.Length > 1 ? pair[1] : string.Empty
            };

            foreach (var segment in segments.Skip(1))
            {
                var attribute = segment.Split('=', 2);
                var name = attribute[0];
                var attributeValue = attribute.Length > 1 ? attribute[1] : null;

                if (string.Equals(name, "Domain", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Domain = attributeValue;
                }
                else if (string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Path = attributeValue;
                }
                else if (string.Equals(name, "Expires", StringComparison.OrdinalIgnoreCase)
                    && DateTimeOffset.TryParse(attributeValue, out var expires))
                {
                    cookie.Expires = expires;
                }
                else if (string.Equals(name, "Max-Age", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(attributeValue, out var maxAge))
                {
                    cookie.Expires = DateTimeOffset.UtcNow.AddSeconds(maxAge);
                }
                else if (string.Equals(name, "Secure", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.Secure = true;
                }
                else if (string.Equals(name, "HttpOnly", StringComparison.OrdinalIgnoreCase))
                {
                    cookie.HttpOnly = true;
                }
            }

            return cookie;
        }).ToList();
    }

    private static async Task<string> CreateRawRequestPreviewAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append(request.Method).Append(' ').Append(request.RequestUri).AppendLine(" HTTP/1.1");
        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
            {
                builder.Append(header.Key).Append(": ").AppendLine(value);
            }
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                foreach (var value in header.Value)
                {
                    builder.Append(header.Key).Append(": ").AppendLine(value);
                }
            }

            builder.AppendLine();
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            builder.Append(body);
        }

        return builder.ToString();
    }

    private static ReadableStreamFormat ResolveStreamFormat(ReadableStreamFormat format, MediaTypeHeaderValue? contentType)
    {
        if (format != ReadableStreamFormat.Auto)
        {
            return format;
        }

        var mediaType = contentType?.MediaType;
        if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return ReadableStreamFormat.ServerSentEvents;
        }

        if (mediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
            || mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ReadableStreamFormat.Lines;
        }

        return ReadableStreamFormat.Raw;
    }

    private static async IAsyncEnumerable<ReadableStreamMessage> ReadRawStreamAsync(
        Stream stream,
        int bufferSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Max(bufferSize, 1024)];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                yield break;
            }

            yield return new ReadableStreamMessage
            {
                Type = ReadableStreamMessageType.Data,
                Data = Encoding.UTF8.GetString(buffer, 0, read)
            };
        }
    }

    private static async IAsyncEnumerable<ReadableStreamMessage> ReadLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            yield return new ReadableStreamMessage
            {
                Type = ReadableStreamMessageType.Data,
                Data = line,
                Raw = line
            };
        }
    }

    private static async IAsyncEnumerable<ReadableStreamMessage> ReadServerSentEventsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string? eventName = null;
        string? eventId = null;
        var data = new StringBuilder();
        var raw = new StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            raw.AppendLine(line);
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return new ReadableStreamMessage
                    {
                        Type = ReadableStreamMessageType.Data,
                        Event = eventName,
                        Id = eventId,
                        Data = data.ToString().TrimEnd('\n'),
                        Raw = raw.ToString()
                    };
                }

                eventName = null;
                eventId = null;
                data.Clear();
                raw.Clear();
                continue;
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            var separator = line.IndexOf(':', StringComparison.Ordinal);
            var field = separator >= 0 ? line[..separator] : line;
            var value = separator >= 0 ? line[(separator + 1)..].TrimStart(' ') : string.Empty;

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "id":
                    eventId = value;
                    break;
                case "data":
                    data.Append(value).Append('\n');
                    break;
            }
        }

        if (data.Length > 0)
        {
            yield return new ReadableStreamMessage
            {
                Type = ReadableStreamMessageType.Data,
                Event = eventName,
                Id = eventId,
                Data = data.ToString().TrimEnd('\n'),
                Raw = raw.ToString()
            };
        }
    }

    private static string? TryReadText(byte[] bytes, MediaTypeHeaderValue? contentType)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var mediaType = contentType?.MediaType;
        var isText = mediaType is null
            || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("html", StringComparison.OrdinalIgnoreCase);

        if (!isText)
        {
            return null;
        }

        var encoding = TryGetEncoding(contentType?.CharSet) ?? Encoding.UTF8;
        return encoding.GetString(bytes);
    }

    private static Encoding? TryGetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record ReadableSendResult(
        HttpResponseMessage Response,
        List<ReadableRedirect> Redirects,
        string RawRequestPreview);
}
