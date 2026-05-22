# FluentHttp.Json

一个轻量的 `HttpClient` fluent 封装。JSON 和 Form 调用被拆成独立客户端，创建入口由 `FluentHttpClient` 负责。

## Install

```CSharp
dotnet add package FluentHttp.Json
```

## JSON Client

```CSharp
var jsonClient = FluentHttpClient.CreateJson("https://www.example.com", TimeSpan.FromMinutes(1))
    .AddBearerAuthentication("xx-xxx")
    .AddHeaders(("X-App", "demo"));

var result = await jsonClient.GetAsync<Dictionary<string, object>>("/hello");
```

```CSharp
var result = await jsonClient.PostAsync<object, string>(
    url: "/hello".AppendUrl(("key1", value1), ("key2", value2)),
    body: new
    {
        Question = "今天天气怎么样"
    });
```

## Form Client

```CSharp
var formClient = FluentHttpClient.CreateForm("https://www.example.com")
    .AddBasicAuthentication("demo", "123456");

var result = await formClient.PostAsync<object, string>(
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
    .PostStreamAsync<object, string>(
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

## Compatibility

原有的 `FluentHttpClient.Create()`、`ReadJsonAsync`、`ReadFormAsync`、`ReadStreamAsync`、`GetFromJsonAsync`、`PostFromJsonAsync` 等 `HttpClient` 扩展仍然可用。新项目建议优先使用 `CreateJson` 和 `CreateForm`。
