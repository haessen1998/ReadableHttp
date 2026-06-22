namespace ReadableHttp.Core;

public sealed class ReadableExchange
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public ReadableRequest Request { get; set; } = new();

    public string? RawRequestPreview { get; set; }

    public ReadableResponse? Response { get; set; }

    public ReadableExecutionError? Error { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public List<ReadableExchangeTiming> Timings { get; set; } = [];
}

public sealed class ReadableExecutionError
{
    public string Type { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class ReadableExchangeTiming
{
    public string Name { get; set; } = string.Empty;

    public TimeSpan StartOffset { get; set; }

    public TimeSpan Duration { get; set; }
}
