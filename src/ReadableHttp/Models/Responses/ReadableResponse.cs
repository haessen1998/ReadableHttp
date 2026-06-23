namespace ReadableHttp;

public sealed class ReadableResponse
{
    public int StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public List<ReadableNameValue> Headers { get; set; } = [];

    public List<ReadableCookie> Cookies { get; set; } = [];

    public List<ReadableRedirect> Redirects { get; set; } = [];

    public string? BodyText { get; set; }

    public byte[]? BodyBytes { get; set; }

    public string? ContentType { get; set; }

    public TimeSpan Duration { get; set; }

    public long? Size { get; set; }
}

public sealed class ReadableCookie
{
    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Domain { get; set; }

    public string? Path { get; set; }

    public DateTimeOffset? Expires { get; set; }

    public bool Secure { get; set; }

    public bool HttpOnly { get; set; }
}

public sealed class ReadableRedirect
{
    public int StatusCode { get; set; }

    public string? Location { get; set; }
}
