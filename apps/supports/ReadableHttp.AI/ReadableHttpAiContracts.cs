using ReadableHttp;

namespace ReadableHttp.AI;

public interface IReadableHttpAiAssistant
{
    Task<ReadableAiVariableSuggestion> GenerateVariableAsync(
        ReadableAiVariableRequest request,
        CancellationToken cancellationToken = default);

    Task<ReadableAiResponseExplanation> ExplainResponseAsync(
        ReadableAiExchangeContext context,
        CancellationToken cancellationToken = default);

    Task<ReadableAiRequestAdjustment> SuggestRequestAdjustmentAsync(
        ReadableAiExchangeContext context,
        CancellationToken cancellationToken = default);

    Task<ReadableAiTrafficAnalysis> AnalyzeTrafficAsync(
        ReadableAiTrafficAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ReadableAiVariableRequest
{
    public string Name { get; set; } = string.Empty;

    public ReadableVariable Variable { get; set; } = new();

    public IReadOnlyDictionary<string, ReadableVariable> Variables { get; set; } =
        new Dictionary<string, ReadableVariable>();

    public ReadableRequest? Request { get; set; }

    public IReadOnlyList<ReadableExchange> History { get; set; } = [];
}

public sealed class ReadableAiVariableSuggestion
{
    public string Value { get; set; } = string.Empty;

    public string? Rationale { get; set; }

    public string? BusinessMeaning { get; set; }
}

public sealed class ReadableAiExchangeContext
{
    public ReadableRequest Request { get; set; } = new();

    public ReadableExchange? Exchange { get; set; }

    public IReadOnlyList<ReadableExchange> RelatedHistory { get; set; } = [];
}

public sealed class ReadableAiResponseExplanation
{
    public string Summary { get; set; } = string.Empty;

    public List<string> Findings { get; set; } = [];

    public List<string> FollowUps { get; set; } = [];
}

public sealed class ReadableAiRequestAdjustment
{
    public string Summary { get; set; } = string.Empty;

    public List<ReadableAiParameterChange> Changes { get; set; } = [];
}

public sealed class ReadableAiParameterChange
{
    public string Location { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? CurrentValue { get; set; }

    public string? SuggestedValue { get; set; }

    public string? Reason { get; set; }
}

public sealed class ReadableAiTrafficAnalysisRequest
{
    public IReadOnlyList<ReadableRequest> Requests { get; set; } = [];

    public IReadOnlyList<ReadableExchange> Exchanges { get; set; } = [];
}

public sealed class ReadableAiTrafficAnalysis
{
    public string Summary { get; set; } = string.Empty;

    public List<string> Characteristics { get; set; } = [];

    public List<string> OptimizationIdeas { get; set; } = [];

    public List<string> Risks { get; set; } = [];
}
