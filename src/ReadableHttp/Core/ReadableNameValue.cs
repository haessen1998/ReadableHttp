namespace ReadableHttp.Core;

public sealed class ReadableNameValue
{
    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }

    public bool Enabled { get; set; } = true;

    public string? Description { get; set; }
}
