using ReadableHttp;

namespace ReadableHttp.AI;

public interface IReadableHttpAiAgent
{
    Task<ReadableAiTurnResult> SendAsync(
        ReadableAiTurnRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReadableHttpAiTool
{
    string Name { get; }

    string Description { get; }

    ReadableAiToolSafety Safety { get; }
}

public interface IReadableHttpAiToolRegistry
{
    IReadOnlyList<IReadableHttpAiTool> Tools { get; }
}

public interface IReadableHttpAiConfirmationPolicy
{
    ReadableAiConfirmationDecision Evaluate(ReadableAiAction action);
}

public sealed class ReadableAiTurnRequest
{
    public string Prompt { get; set; } = string.Empty;

    public ReadableAiWorkspaceContext Context { get; set; } = new();

    public IReadOnlyList<ReadableAiChatMessage> Messages { get; set; } = [];
}

public sealed class ReadableAiTurnResult
{
    public string AssistantMessage { get; set; } = string.Empty;

    public List<ReadableAiAction> Actions { get; set; } = [];

    public List<ReadableAiFinding> Findings { get; set; } = [];
}

public sealed class ReadableAiWorkspaceContext
{
    public string WorkspaceName { get; set; } = string.Empty;

    public string WorkspacePath { get; set; } = string.Empty;

    public ReadableRequest? CurrentRequest { get; set; }

    public ReadableExchange? CurrentExchange { get; set; }

    public IReadOnlyList<ReadableExchange> RecentExchanges { get; set; } = [];

    public IReadOnlyList<ReadableCollection> Collections { get; set; } = [];

    public IReadOnlyList<ReadableSpecification> Specifications { get; set; } = [];

    public string? SelectedText { get; set; }

    public string ViewMode { get; set; } = "request";
}

public sealed class ReadableAiChatMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class ReadableAiFinding
{
    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";
}

public sealed class ReadableAiAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public ReadableAiActionKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public ReadableAiRequestPatch? RequestPatch { get; set; }

    public ReadableAiConfirmationDecision Confirmation { get; set; } = new();
}

public enum ReadableAiActionKind
{
    None,
    UpdateCurrentRequest,
    CreateRequest,
    ShowSearchResults,
    CompareHistory,
    ExplainResponse
}

public sealed class ReadableAiRequestPatch
{
    public string? Method { get; set; }

    public string? Url { get; set; }

    public string? BodyText { get; set; }

    public string? BodyType { get; set; }

    public string? BodyContentType { get; set; }

    public List<ReadableAiParameterChange> QueryChanges { get; set; } = [];

    public List<ReadableAiParameterChange> HeaderChanges { get; set; } = [];

    public List<ReadableAiParameterChange> FormChanges { get; set; } = [];
}

public sealed class ReadableAiConfirmationDecision
{
    public bool RequiresConfirmation { get; set; } = true;

    public string Reason { get; set; } = "AI generated a request change.";

    public ReadableAiToolSafety Safety { get; set; } = ReadableAiToolSafety.WriteModel;
}

public enum ReadableAiToolSafety
{
    ReadOnly,
    Draft,
    WriteModel,
    ExternalEffect,
    Destructive
}

public sealed class ReadableAiToolDefinition : IReadableHttpAiTool
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ReadableAiToolSafety Safety { get; set; } = ReadableAiToolSafety.ReadOnly;
}

public sealed class ReadableAiParameterChange
{
    public string Location { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? CurrentValue { get; set; }

    public string? SuggestedValue { get; set; }

    public string? Reason { get; set; }
}
