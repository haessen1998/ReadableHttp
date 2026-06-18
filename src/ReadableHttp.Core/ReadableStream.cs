namespace ReadableHttp.Core;

public enum ReadableStreamFormat
{
    Auto,
    ServerSentEvents,
    Lines,
    Raw
}

public enum ReadableStreamMessageType
{
    Headers,
    Data,
    Error,
    Completed
}

public sealed class ReadableStreamOptions
{
    public ReadableStreamFormat Format { get; set; } = ReadableStreamFormat.Auto;

    public int BufferSize { get; set; } = 8192;
}

public sealed class ReadableStreamMessage
{
    public ReadableStreamMessageType Type { get; set; } = ReadableStreamMessageType.Data;

    public int? StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public List<ReadableNameValue> Headers { get; set; } = [];

    public string? Event { get; set; }

    public string? Id { get; set; }

    public string? Data { get; set; }

    public string? Raw { get; set; }

    public ReadableExecutionError? Error { get; set; }
}
