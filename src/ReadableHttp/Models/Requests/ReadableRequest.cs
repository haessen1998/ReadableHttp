namespace ReadableHttp;

public sealed class ReadableRequest
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Untitled Request";

    public string Method { get; set; } = "GET";

    public string Url { get; set; } = string.Empty;

    public List<ReadableNameValue> PathParameters { get; set; } = [];

    public List<ReadableNameValue> Query { get; set; } = [];

    public List<ReadableNameValue> Headers { get; set; } = [];

    public ReadableBody? Body { get; set; }

    public ReadableAuth? Auth { get; set; }

    public ReadableRequestOptions Options { get; set; } = new();

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];
}

public sealed class ReadableRequestOptions
{
    public TimeSpan? Timeout { get; set; }

    public bool? FollowRedirects { get; set; }

    public bool? UseCookies { get; set; }

    public bool? IgnoreSslErrors { get; set; }

    public ReadableProxyOptions? Proxy { get; set; }
}

public enum ReadableProxyMode
{
    System,
    None,
    Custom
}

public sealed class ReadableProxyOptions
{
    public ReadableProxyMode Mode { get; set; } = ReadableProxyMode.System;

    public string? Url { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
