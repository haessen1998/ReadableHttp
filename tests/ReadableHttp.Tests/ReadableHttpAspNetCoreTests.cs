using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ReadableHttp.AspNetCore;
using ReadableHttp.Core;
using ReadableHttp.Execution;

namespace ReadableHttp.Tests;

public sealed class ReadableHttpAspNetCoreTests
{
    [Fact]
    public async Task AddReadableHttp_registers_executor_with_http_client_factory()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}")));
        var services = new ServiceCollection();

        services
            .AddHttpClient(ReadableHttpClientNames.Default)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddReadableHttp();

        await using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IReadableHttpExecutor>();

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "https://api.example.test/factory"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Single(handler.Requests);
        Assert.Equal("https://api.example.test/factory", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task AddReadableHttp_configures_default_named_client()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("https://api.example.test/configured"), request.RequestUri);
            return Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        });
        var services = new ServiceCollection();

        services
            .AddHttpClient(ReadableHttpClientNames.Default)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddReadableHttp(client =>
        {
            client.BaseAddress = new Uri("https://api.example.test");
        });

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IReadableHttpFactory>();
        var executor = factory.CreateExecutor();

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "/configured"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Factory_creates_executor_for_named_http_client()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("https://named.example.test/ping"), request.RequestUri);
            return Task.FromResult(MockHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        });
        var services = new ServiceCollection();

        services.AddReadableHttp();
        services
            .AddHttpClient("named-api", client =>
            {
                client.BaseAddress = new Uri("https://named.example.test");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IReadableHttpFactory>();
        var executor = factory.CreateExecutor("named-api");

        var exchange = await executor.SendAsync(
            new ReadableRequest
            {
                Method = "GET",
                Url = "/ping"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(exchange.Error);
        Assert.Single(handler.Requests);
    }
}
