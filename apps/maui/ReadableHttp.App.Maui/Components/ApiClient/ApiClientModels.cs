using ReadableHttp;

namespace ReadableHttp.App.Maui.Components.ApiClient;

public sealed record ActivityEntry(string Title, string Detail);

public sealed record PipelinePhase(string Label, int Width, string Color, string Duration, int ElapsedMs);

public sealed record AiChatMessage(string Role, string Content);

public sealed record EditableNameValueChange(string Kind, int Index, string Field, string? Value);

public sealed record VariableChange(string Scope, int Index, string Field, string? Value);

public sealed record EnvironmentChange(string EnvironmentId, string Field, string? Value);

public sealed record AuthChange(string Field, string? Value);

public sealed record AppSettingsDraft(
    string ThemeMode = "system",
    string ProxyMode = "system",
    string CustomProxyUrl = "",
    string CustomProxyUsername = "",
    string CustomProxyPassword = "",
    string Language = "system",
    string FontSize = "medium",
    bool DevToolsEnabled = true);

public enum RequestTabOrigin
{
    Scratch,
    Workspace,
    CollectionConfig,
    Collection,
    Specification,
    SpecificationConfig,
    SpecificationDocument,
    About,
    Settings
}

public sealed class RequestWorkspaceTab
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "Scratch Request";

    public RequestTabOrigin Origin { get; set; } = RequestTabOrigin.Scratch;

    public string? CollectionId { get; set; }

    public string? RequestId { get; set; }

    public string? SourceKey { get; set; }

    public string ActiveSource { get; set; } = "Request";

    public string Method { get; set; } = "GET";

    public string Url { get; set; } = "https://httpbin.org/get";

    public string BodyText { get; set; } = string.Empty;

    public ReadableBodyType BodyType { get; set; } = ReadableBodyType.None;

    public string BodyContentType { get; set; } = string.Empty;

    public List<ReadableNameValue> Query { get; set; } = [];

    public List<ReadableNameValue> Headers { get; set; } = [];

    public List<ReadableNameValue> Form { get; set; } = [];

    public ReadableAuth? Auth { get; set; }

    public string StatusText { get; set; } = "Ready";

    public string ResponseText { get; set; } = string.Empty;

    public string ResponseNodePath { get; set; } = "root";

    public string RequestSectionTab { get; set; } = "Params";

    public bool IsDirty { get; set; }

    public List<PipelinePhase> PipelinePhases { get; set; } = [];
}

public enum RightPanelMode
{
    Docs,
    Ai,
    Parameters,
    Settings
}

public sealed record WorkspaceSelectedEventArgs(string Path);

public sealed record SpecSelectedEventArgs(string Path);

public sealed record SessionSelectedEventArgs(string Title);

public sealed record ResponseNodeSelectedEventArgs(string Path);

public sealed record CollectionEventArgs(ReadableCollection Collection);

public sealed record RequestEventArgs(ReadableCollection Collection, ReadableRequest Request);

public sealed record RequestMoveEventArgs(ReadableCollection SourceCollection, ReadableRequest Request, ReadableCollection TargetCollection);

public sealed record SpecificationEventArgs(ReadableSpecification Specification);
