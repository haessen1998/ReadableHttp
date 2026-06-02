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
var result = await jsonClient.PostAsync<string>(
    url: "/hello".Query(new { key1 = value1, key2 = value2 }),
    body: new
    {
        Question = "今天天气怎么样"
    });
```

Query 也支持字典和具名元组：

```CSharp
"/hello".Query(new Dictionary<string, object?>
{
    ["page"] = 1,
    ["tags"] = new[] { "a", "b" }
});

"/hello".Query(("page", 1), ("size", 20));
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

## Compatibility

原有的 `FluentHttpClient.Create()`、`ReadJsonAsync`、`ReadFormAsync`、`ReadStreamAsync`、`GetFromJsonAsync`、`PostFromJsonAsync` 等 `HttpClient` 扩展仍然可用。`PostAsync`、`PostFromJsonAsync`、`PostFromFormAsync` 和 `PostStreamAsync` 支持只指定响应类型，body 可以直接传匿名类或 `Dictionary`。新项目建议优先使用 `CreateJson`、`CreateForm` 和 `Query`。
