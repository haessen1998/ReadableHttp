using System.Net;
using System.Net.Http.Headers;
using ReadableHttp;
using ReadableHttp.Execution;

namespace ReadableHttp.Tests;

public sealed class ReadableHttpExecutorTests
{
    [Fact]
    public async Task SendAsync_resolves_variables_and_query_values()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}")));
        var executor = new ReadableHttpExecutor(handler);

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "{{baseUrl}}/users/{{userId}}",
                Query =
                [
                    new ReadableNameValue { Name = "page", Value = "{{page}}" }
                ]
            },
            new ReadableExecutionContext
            {
                Variables =
                {
                    ["baseUrl"] = "https://api.example.test",
                    ["userId"] = "42",
                    ["page"] = "3"
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Equal(200, exchange.Response?.StatusCode);
        Assert.Equal("https://api.example.test/users/42?page=3", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task SendAsync_resolves_variables_only_in_json_string_values()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "{{baseUrl}}/users",
                Headers =
                [
                    new ReadableNameValue { Name = "x-template", Value = "{{value}}" }
                ],
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    Content = "{\"{{propertyName}}\":\"{{value}}\"}"
                }
            },
            new ReadableExecutionContext
            {
                Variables =
                {
                    ["baseUrl"] = "https://api.example.test",
                    ["propertyName"] = "should-not-change-property-names",
                    ["value"] = "resolved"
                }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("https://api.example.test/users", handler.Requests[0].RequestUri?.ToString());
        Assert.True(handler.Requests[0].Headers.TryGetValues("x-template", out var values));
        Assert.Equal("resolved", Assert.Single(values));
        Assert.Equal("{\"{{propertyName}}\":\"resolved\"}", await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_preserves_non_success_responses()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}")));
        var executor = new ReadableHttpExecutor(handler);

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/fail",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    Content = "{\"name\":\"demo\"}"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Equal(400, exchange.Response?.StatusCode);
        Assert.Contains("\"bad\"", exchange.Response?.BodyText);
    }

    [Fact]
    public async Task SendAsync_applies_basic_bearer_and_api_key_auth()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/basic",
                Auth = new ReadableAuth
                {
                    Type = ReadableAuthType.Basic,
                    Username = "demo",
                    Password = "secret"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/bearer",
                Auth = new ReadableAuth
                {
                    Type = ReadableAuthType.Bearer,
                    Token = "token"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/key",
                Auth = new ReadableAuth
                {
                    Type = ReadableAuthType.ApiKey,
                    Name = "x-api-key",
                    Value = "key",
                    ApiKeyLocation = ReadableApiKeyLocation.Header
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Basic", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization?.Scheme);
        Assert.True(handler.Requests[2].Headers.TryGetValues("x-api-key", out var values));
        Assert.Equal("key", Assert.Single(values));
    }

    [Fact]
    public async Task SendAsync_does_not_mutate_request_when_auth_uses_query()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);
        var request = new ReadableRequest
        {
            Method = "GET",
            Url = "https://api.example.test/key",
            Auth = new ReadableAuth
            {
                Type = ReadableAuthType.ApiKey,
                Name = "api_key",
                Value = "secret",
                ApiKeyLocation = ReadableApiKeyLocation.Query
            }
        };

        await executor.SendAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        await executor.SendAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(request.Query);
        Assert.Equal("https://api.example.test/key?api_key=secret", handler.Requests[0].RequestUri?.ToString());
        Assert.Equal("https://api.example.test/key?api_key=secret", handler.Requests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task SendAsync_writes_json_raw_and_form_bodies()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/json",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    Content = "{\"hello\":\"world\"}"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "PATCH",
                Url = "https://api.example.test/raw",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Raw,
                    ContentType = "text/plain",
                    Content = "hello"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/form",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.FormUrlEncoded,
                    Form =
                    [
                        new ReadableNameValue { Name = "a", Value = "1" },
                        new ReadableNameValue { Name = "b", Value = "2" }
                    ]
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{\"hello\":\"world\"}", await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("hello", await handler.Requests[1].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("a=1&b=2", await handler.Requests[2].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_writes_semantic_raw_and_graphql_bodies()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/html",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Html,
                    Content = "<strong>Hello</strong>"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/graphql",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Graphql,
                    Graphql = new ReadableGraphqlBody
                    {
                        Query = "query User($id: ID!) { user(id: $id) { name } }",
                        Variables = "{\"id\":\"42\"}",
                        OperationName = "User"
                    }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("text/html; charset=utf-8", handler.Requests[0].Content!.Headers.ContentType?.ToString());

        var graphql = await handler.Requests[1].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"operationName\":\"User\"", graphql);
        Assert.Contains("\"variables\":{\"id\":\"42\"}", graphql);
        Assert.Equal("application/json; charset=utf-8", handler.Requests[1].Content!.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task StreamAsync_reads_server_sent_events()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("event: message\nid: 1\ndata: {\"answer\":\"hi\"}\n\n")
            };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/event-stream");
            return Task.FromResult(response);
        });
        var executor = new ReadableHttpExecutor(handler);

        var messages = new List<ReadableStreamMessage>();
        await foreach (var message in executor.StreamAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/events"
            },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            messages.Add(message);
        }

        Assert.Equal(ReadableStreamMessageType.Headers, messages[0].Type);
        var data = Assert.Single(messages, message => message.Type == ReadableStreamMessageType.Data);
        Assert.Equal("message", data.Event);
        Assert.Equal("1", data.Id);
        Assert.Equal("{\"answer\":\"hi\"}", data.Data);
        Assert.Equal(ReadableStreamMessageType.Completed, messages[^1].Type);
    }

    [Fact]
    public async Task SendAsync_preserves_factory_client_timeout_when_timeout_is_not_explicit()
    {
        HttpClient? client = null;
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            Assert.Equal(TimeSpan.FromSeconds(7), client!.Timeout);
            return Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        });
        var executor = new ReadableHttpExecutor(() =>
        {
            client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(7)
            };
            return client;
        });

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/timeout"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
    }

    [Fact]
    public async Task StreamAsync_applies_request_timeout_option()
    {
        HttpClient? client = null;
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            Assert.Equal(TimeSpan.FromSeconds(9), client!.Timeout);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("line 1\n")
            };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            return Task.FromResult(response);
        });
        var executor = new ReadableHttpExecutor(() =>
        {
            client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(7)
            };
            return client;
        });

        var messages = new List<ReadableStreamMessage>();
        await foreach (var message in executor.StreamAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/stream",
                Options = new ReadableRequestOptions
                {
                    Timeout = TimeSpan.FromSeconds(9)
                }
            },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            messages.Add(message);
        }

        Assert.Contains(messages, message => message.Type == ReadableStreamMessageType.Data);
    }

    [Fact]
    public async Task SendAsync_applies_path_parameters_and_reads_cookies()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "session=abc; Domain=api.example.test; Path=/; Expires=Wed, 17 Jun 2026 12:00:00 GMT; Secure; HttpOnly");
            return Task.FromResult(response);
        });
        var executor = new ReadableHttpExecutor(handler);

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/users/{id}",
                PathParameters =
                [
                    new ReadableNameValue { Name = "id", Value = "42" }
                ]
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://api.example.test/users/42", handler.Requests[0].RequestUri?.ToString());
        var cookie = Assert.Single(exchange.Response!.Cookies);
        Assert.Equal("session", cookie.Name);
        Assert.Equal("api.example.test", cookie.Domain);
        Assert.Equal("/", cookie.Path);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.NotNull(cookie.Expires);
        Assert.Contains("GET https://api.example.test/users/42", exchange.RawRequestPreview);
    }

    [Fact]
    public async Task SendAsync_records_redirect_chain()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri?.AbsolutePath == "/old")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers =
                    {
                        Location = new Uri("/new", UriKind.Relative)
                    }
                });
            }

            return Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"));
        });
        var executor = new ReadableHttpExecutor(handler);

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "POST",
                Url = "https://api.example.test/old",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    Content = "{\"name\":\"demo\"}"
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Equal(200, exchange.Response?.StatusCode);
        var redirect = Assert.Single(exchange.Response!.Redirects);
        Assert.Equal(302, redirect.StatusCode);
        Assert.Equal("https://api.example.test/new", redirect.Location);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Null(handler.Requests[1].Content);
    }

    [Fact]
    public async Task SendAsync_applies_oauth2_header_token()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/secure",
                Auth = new ReadableAuth
                {
                    Type = ReadableAuthType.OAuth2,
                    OAuth2 = new ReadableOAuth2Options
                    {
                        ExtraParameters =
                        {
                            ["credentials"] = "token"
                        }
                    }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handler.Requests[0].Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Bearer token", Assert.Single(values));
    }

    [Fact]
    public async Task SendAsync_applies_oauth1_authorization_header()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var executor = new ReadableHttpExecutor(handler);

        await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/secure",
                Auth = new ReadableAuth
                {
                    Type = ReadableAuthType.OAuth1,
                    OAuth1 = new ReadableOAuth1Options
                    {
                        ConsumerKey = "key",
                        ConsumerSecret = "secret"
                    }
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handler.Requests[0].Headers.TryGetValues("Authorization", out var values));
        Assert.Contains("oauth_signature", Assert.Single(values));
    }
}
