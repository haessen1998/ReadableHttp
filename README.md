# FluentHttp

一个轻量的 `HttpClient` fluent 封装，用于更清晰地创建 JSON、Form 请求客户端，并通过 `IHttpClientFactory` 接入依赖注入。

## Packages

```CSharp
dotnet add package FluentHttp.Json
dotnet add package FluentHttpFactory
```

`FluentHttp.Json` 提供 JSON/Form 专用客户端和 `HttpClient` 扩展方法。

`FluentHttpFactory` 提供基于 `IHttpClientFactory` 的创建器，适合 ASP.NET Core / Worker Service 等 DI 场景。

## JSON Client

```CSharp
var jsonClient = FluentHttpClient.CreateJson("https://www.example.com", TimeSpan.FromMinutes(1))
    .AddBearerAuthentication("xx-xxx")
    .AddHeaders(("X-App", "demo"));

var result = await jsonClient.GetAsync<Dictionary<string, object>>("/hello");
```

### Query

```CSharp
var result = await jsonClient.GetAsync<Dictionary<string, object>>(
    url: "/hello",
    query: new
    {
        page = 1,
        size = 20,
        keyword = "demo"
    });
```

```CSharp
"/hello".Query(new Dictionary<string, object?>
{
    ["page"] = 1,
    ["tags"] = new[] { "a", "b" }
});

"/hello".Query(("page", 1), ("size", 20));
```

### POST JSON

```CSharp
var result = await jsonClient.PostAsync<string>(
    url: "/hello".Query(new { key1 = value1, key2 = value2 }),
    body: new
    {
        Question = "今天天气怎么样"
    });
```

## Form Client

```CSharp
var formClient = FluentHttpClient.CreateForm("https://www.example.com")
    .AddBasicAuthentication("demo", "123456");

var result = await formClient.PostAsync<string>(
    url: "/login",
    body: new
    {
        UserName = "demo",
        Password = "123456"
    });
```

## Streaming JSON

```CSharp
var stream = FluentHttpClient.CreateJson("https://www.example.com", TimeSpan.FromMinutes(1))
    .AddBearerAuthentication("xx-xxx")
    .PostStreamAsync<string>(
        url: "/chat",
        body: bodyArguments,
        streamType: FluentHttpExtensions.EventStream);

await foreach (var message in stream)
{
    Console.WriteLine(message);
}
```

## Cookie

```CSharp
var jsonClient = FluentHttpClient.CreateJsonWithCookie(
    baseUrl: "https://www.example.com",
    timeout: TimeSpan.FromMinutes(1),
    ("session", "xxxxx"));
```

## Dependency Injection

```CSharp
builder.Services.AddFluentHttpFactory();

builder.Services.AddHttpClient("example", client =>
{
    client.BaseAddress = new Uri("https://www.example.com");
    client.Timeout = TimeSpan.FromMinutes(1);
});
```

```CSharp
public class DemoService
{
    private readonly IFluentHttpFactory _fluentHttpFactory;

    public DemoService(IFluentHttpFactory fluentHttpFactory)
    {
        _fluentHttpFactory = fluentHttpFactory;
    }

    public async Task<Dictionary<string, object>> GetAsync()
    {
        var jsonClient = _fluentHttpFactory.CreateJson("example")
            .AddBearerAuthentication("xx-xxx");

        return await jsonClient.GetAsync<Dictionary<string, object>>("/hello");
    }
}
```

需要原生能力时，也可以继续使用：

```CSharp
var httpClient = fluentHttpFactory.Create("example");
```

## Compatibility

原有的 `FluentHttpClient.Create()`、`ReadJsonAsync`、`ReadFormAsync`、`ReadStreamAsync`、`GetFromJsonAsync`、`PostFromJsonAsync` 等 `HttpClient` 扩展仍然可用。

`PostAsync`、`PostFromJsonAsync`、`PostFromFormAsync` 和 `PostStreamAsync` 支持只指定响应类型，body 可以直接传匿名类或 `Dictionary`。
