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
            .AddHttpClient(string.Empty)
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
}
