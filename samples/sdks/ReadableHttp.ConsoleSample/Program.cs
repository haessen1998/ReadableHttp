using ReadableHttp;

var exchange = await ReadableHttpClient
    .Request("https://httpbin.org/get")
    .Get()
    .WithHeader("accept", "application/json")
    .WithQuery("source", "console-sample")
    .SendExchangeAsync();

Console.WriteLine($"Status: {exchange.Response?.StatusCode}");
Console.WriteLine(exchange.Response?.BodyText);

Console.WriteLine();
Console.WriteLine("Streaming shape:");

await foreach (var message in ReadableHttpClient
    .Request("https://httpbin.org/stream/3")
    .Get()
    .StreamAsync(ReadableStreamFormat.Lines))
{
    if (message.Type == ReadableStreamMessageType.Data)
    {
        Console.WriteLine(message.Data);
    }
}
