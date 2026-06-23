# ReadableHttp

ReadableHttp is the core SDK package. It contains the request model, response model, execution engine, streaming support, variable resolution, and a small fluent facade for direct .NET usage.

## Install

```shell
dotnet add package ReadableHttp --version 2.0.0
```

## Send A Request

```csharp
using ReadableHttp;

var exchange = await ReadableHttpClient
    .Request("https://api.example.com/users/42")
    .Get()
    .WithHeader("accept", "application/json")
    .SendExchangeAsync();

Console.WriteLine(exchange.Response?.StatusCode);
Console.WriteLine(exchange.Response?.BodyText);
```

## Use The Execution Model Directly

```csharp
using ReadableHttp;
using ReadableHttp.Execution;

var request = new ReadableRequest
{
    Method = "GET",
    Url = "{{baseUrl}}/users/{{userId}}",
    Query =
    [
        new ReadableNameValue { Name = "include", Value = "profile" }
    ]
};

var context = new ReadableExecutionContext
{
    Variables =
    {
        ["baseUrl"] = "https://api.example.com",
        ["userId"] = "42"
    }
};

var exchange = await new ReadableHttpExecutor()
    .SendExchangeAsync(request, context);
```

## Streaming

```csharp
using ReadableHttp;

await foreach (var message in ReadableHttpClient
    .Request("https://api.example.com/events")
    .Get()
    .StreamAsync(ReadableStreamFormat.ServerSentEvents))
{
    if (message.Type == ReadableStreamMessageType.Data)
    {
        Console.WriteLine(message.Data);
    }
}
```

## Included File Schemas

The package includes JSON schemas for ReadableHttp request, environment, and workspace files under the `schemas/` package folder.
