using ReadableHttp.AI;

namespace ReadableHttp.AI.MAF;

public sealed class ReadableHttpMafToolRegistry : IReadableHttpAiToolRegistry
{
    public IReadOnlyList<IReadableHttpAiTool> Tools { get; } =
    [
        new ReadableAiToolDefinition
        {
            Name = "get_current_request",
            Description = "Read the currently active request model.",
            Safety = ReadableAiToolSafety.ReadOnly
        },
        new ReadableAiToolDefinition
        {
            Name = "search_workspace_apis",
            Description = "Search collections and specifications for related APIs.",
            Safety = ReadableAiToolSafety.ReadOnly
        },
        new ReadableAiToolDefinition
        {
            Name = "get_recent_exchanges",
            Description = "Read recent request/response exchange history for comparison.",
            Safety = ReadableAiToolSafety.ReadOnly
        },
        new ReadableAiToolDefinition
        {
            Name = "generate_request_patch",
            Description = "Generate a draft request patch for query, headers, body, method, or URL.",
            Safety = ReadableAiToolSafety.Draft
        },
        new ReadableAiToolDefinition
        {
            Name = "apply_request_patch",
            Description = "Apply a confirmed request patch to the current request model.",
            Safety = ReadableAiToolSafety.WriteModel
        }
    ];
}

public sealed class ReadableHttpMafConfirmationPolicy : IReadableHttpAiConfirmationPolicy
{
    public ReadableAiConfirmationDecision Evaluate(ReadableAiAction action)
    {
        var safety = action.Kind switch
        {
            ReadableAiActionKind.UpdateCurrentRequest => ReadableAiToolSafety.WriteModel,
            ReadableAiActionKind.CreateRequest => ReadableAiToolSafety.WriteModel,
            _ => ReadableAiToolSafety.ReadOnly
        };

        return new ReadableAiConfirmationDecision
        {
            Safety = safety,
            RequiresConfirmation = safety != ReadableAiToolSafety.ReadOnly,
            Reason = safety == ReadableAiToolSafety.ReadOnly
                ? "This action only reads workspace context."
                : "This action changes the current request model and needs user confirmation."
        };
    }
}
