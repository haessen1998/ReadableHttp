namespace ReadableHttp;

public sealed class ReadableHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string RequestId { get; set; } = string.Empty;

    public string RequestName { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Url { get; set; } = string.Empty;

    public int? StatusCode { get; set; }

    public TimeSpan Duration { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public string? ExchangePath { get; set; }
}
