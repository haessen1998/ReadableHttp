namespace ReadableHttp.Core;

public sealed class ReadableSecretStore
{
    public string WorkspaceId { get; set; } = string.Empty;

    public Dictionary<string, string?> Values { get; set; } = [];
}
