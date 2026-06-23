# ReadableHttp.AspNetCore

ReadableHttp.AspNetCore integrates ReadableHttp with ASP.NET Core dependency injection and `IHttpClientFactory`.

Use this package when you want named `HttpClient` configuration, DI-friendly executors, and request execution that follows your ASP.NET Core service configuration.

## Install

```shell
dotnet add package ReadableHttp.AspNetCore --version 2.0.0
```

## Register ReadableHttp

```csharp
using ReadableHttp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReadableHttp(client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## Execute Requests From DI

```csharp
using ReadableHttp;
using ReadableHttp.Execution;

app.MapGet("/users/{id}", async (
    string id,
    IReadableHttpExecutor executor,
    CancellationToken cancellationToken) =>
{
    var exchange = await executor.SendExchangeAsync(
        new ReadableRequest
        {
            Method = "GET",
            Url = $"/users/{id}"
        },
        cancellationToken: cancellationToken);

    return Results.Json(new
    {
        status = exchange.Response?.StatusCode,
        body = exchange.Response?.BodyText,
        error = exchange.Error?.Message
    });
});
```

## Named Clients

```csharp
builder.Services.AddReadableHttp();
builder.Services.AddHttpClient("billing", client =>
{
    client.BaseAddress = new Uri("https://billing.example.com");
});

app.MapGet("/billing/health", async (
    IReadableHttpFactory factory,
    CancellationToken cancellationToken) =>
{
    var executor = factory.CreateExecutor("billing");
    return await executor.SendExchangeAsync(
        new ReadableRequest { Method = "GET", Url = "/health" },
        cancellationToken: cancellationToken);
});
```
