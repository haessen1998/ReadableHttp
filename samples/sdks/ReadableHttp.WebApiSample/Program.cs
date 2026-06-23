using ReadableHttp.AspNetCore;
using ReadableHttp.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReadableHttp();
builder.Services.AddHttpClient("httpbin", client =>
{
    client.BaseAddress = new Uri("https://httpbin.org");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/httpbin"));

app.MapGet("/httpbin", async (IReadableHttpFactory readableHttpFactory, CancellationToken cancellationToken) =>
{
    var executor = readableHttpFactory.CreateExecutor("httpbin");
    var exchange = await executor.SendAsync(
        new ReadableRequest
        {
            Name = "Get HTTPBin",
            Method = "GET",
            Url = "/get",
            Query =
            [
                new ReadableNameValue
                {
                    Name = "source",
                    Value = "webapi-sample"
                }
            ],
            Headers =
            [
                new ReadableNameValue
                {
                    Name = "accept",
                    Value = "application/json"
                }
            ]
        },
        cancellationToken: cancellationToken);

    return Results.Json(new
    {
        status = exchange.Response?.StatusCode,
        body = exchange.Response?.BodyText,
        error = exchange.Error?.Message
    });
});

app.MapGet("/httpbin/stream", (IReadableHttpFactory readableHttpFactory, CancellationToken cancellationToken) =>
{
    var executor = readableHttpFactory.CreateExecutor("httpbin");
    return executor.StreamAsync(
        new ReadableRequest
        {
            Name = "HTTPBin Stream",
            Method = "GET",
            Url = "/stream/3"
        },
        options: new ReadableStreamOptions
        {
            Format = ReadableStreamFormat.Lines
        },
        cancellationToken: cancellationToken);
});

app.Run();
