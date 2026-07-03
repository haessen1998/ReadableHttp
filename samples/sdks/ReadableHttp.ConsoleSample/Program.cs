using ReadableHttp;

//var exchange = await ReadableHttpClient
//    .Request("https://httpbin.org/get")
//    .Get()
//    .WithHeader("accept", "application/json")
//    .WithQuery("source", "console-sample")
//    .SendExchangeAsync();

//Console.WriteLine($"Status: {exchange.Response?.StatusCode}");
//Console.WriteLine(exchange.Response?.BodyText);

Console.WriteLine();
Console.WriteLine("JsonArray shape:");

await foreach (var message in ReadableHttpClient
    .Request("https://intelligentapi.hzfanews.com/Ai/AnswerStrings")
    .WithQuery("modelEnum", 12)
    .Post()
    .WithJsonBody(new
    {
        Question = "介绍一下杭州"
    })
    .StreamAsync(ReadableStreamFormat.Auto))
{
    if (message.Type == ReadableStreamMessageType.Data)
    {
        Console.Write(message.Data);
    }
}


Console.WriteLine();
Console.WriteLine("Streaming shape:");
await foreach (var message in ReadableHttpClient
    .Request("https://intelligentapi.hzfanews.com/Ai/AnswerStream?modelEnum=12")
    .Post()
    .WithJsonBody(new
    {
        Question = "介绍一下杭州"
    })
    .StreamAsync(ReadableStreamFormat.Auto))
{
    if (message.Type == ReadableStreamMessageType.Data)
    {
        Console.Write(message.Data);
    }
}
