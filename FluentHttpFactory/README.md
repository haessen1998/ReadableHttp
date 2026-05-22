# FluentHttpFactory

基于 `IHttpClientFactory` 的创建器。它保留 raw `HttpClient` 创建能力，同时可以直接创建 JSON/Form 专用客户端。

## Install

```CSharp
dotnet add package FluentHttpFactory
```

## Register

```CSharp
builder.Services.AddFluentHttpFactory();

builder.Services.AddHttpClient("example", client =>
{
    client.BaseAddress = new Uri("https://www.example.com");
    client.Timeout = TimeSpan.FromMinutes(1);
});
```

## Use JSON Client

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

## Use Form Client

```CSharp
var formClient = fluentHttpFactory.CreateForm("example");

var result = await formClient.PostAsync<object, string>(
    url: "/login",
    body: new
    {
        UserName = "demo",
        Password = "123456"
    });
```

需要原生能力时，也可以继续使用 `fluentHttpFactory.Create("example")` 获取 `HttpClient`。
