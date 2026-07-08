using ReadableHttp.AI;

namespace ReadableHttp.AI.MAF;

public sealed class ReadableHttpMafAgent : IReadableHttpAiAgent
{
    private readonly IReadableHttpAiToolRegistry _tools;
    private readonly IReadableHttpAiConfirmationPolicy _confirmationPolicy;

    public ReadableHttpMafAgent(
        IReadableHttpAiToolRegistry tools,
        IReadableHttpAiConfirmationPolicy confirmationPolicy)
    {
        _tools = tools;
        _confirmationPolicy = confirmationPolicy;
    }

    public Task<ReadableAiTurnResult> SendAsync(ReadableAiTurnRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt.Trim();
        var context = request.Context;
        var lowerPrompt = prompt.ToLowerInvariant();
        var result = new ReadableAiTurnResult();

        if (lowerPrompt.Contains("参数", StringComparison.Ordinal)
            || lowerPrompt.Contains("parameter", StringComparison.Ordinal)
            || lowerPrompt.Contains("调整", StringComparison.Ordinal)
            || lowerPrompt.Contains("修改", StringComparison.Ordinal)
            || lowerPrompt.Contains("params", StringComparison.Ordinal))
        {
            result.AssistantMessage = BuildParameterMessage(context);
            AddPatchAction(result, BuildParameterPatch(context));
            return Task.FromResult(result);
        }

        if (lowerPrompt.Contains("失败", StringComparison.Ordinal)
            || lowerPrompt.Contains("错误", StringComparison.Ordinal)
            || lowerPrompt.Contains("原因", StringComparison.Ordinal)
            || lowerPrompt.Contains("结果", StringComparison.Ordinal)
            || lowerPrompt.Contains("response", StringComparison.Ordinal)
            || lowerPrompt.Contains("explain", StringComparison.Ordinal)
            || lowerPrompt.Contains("error", StringComparison.Ordinal)
            || lowerPrompt.Contains("fail", StringComparison.Ordinal))
        {
            result.AssistantMessage = BuildResponseExplanation(context);
            result.Actions.Add(ApplyPolicy(new ReadableAiAction
            {
                Kind = ReadableAiActionKind.ExplainResponse,
                Title = "解释当前响应",
                Summary = "读取当前请求和响应快照，分析状态、响应体和可能的失败原因。"
            }));
            return Task.FromResult(result);
        }

        if (lowerPrompt.Contains("对比", StringComparison.Ordinal)
            || lowerPrompt.Contains("最近三次", StringComparison.Ordinal)
            || lowerPrompt.Contains("compare", StringComparison.Ordinal)
            || lowerPrompt.Contains("history", StringComparison.Ordinal))
        {
            result.AssistantMessage = BuildHistoryMessage(context);
            result.Actions.Add(ApplyPolicy(new ReadableAiAction
            {
                Kind = ReadableAiActionKind.CompareHistory,
                Title = "对比最近消息记录",
                Summary = "读取当前请求最近的消息记录并生成差异摘要。"
            }));
            return Task.FromResult(result);
        }

        if (lowerPrompt.Contains("找", StringComparison.Ordinal)
            || lowerPrompt.Contains("搜索", StringComparison.Ordinal)
            || lowerPrompt.Contains("接口", StringComparison.Ordinal)
            || lowerPrompt.Contains("search", StringComparison.Ordinal))
        {
            result.AssistantMessage = BuildSearchMessage(context, prompt);
            result.Actions.Add(ApplyPolicy(new ReadableAiAction
            {
                Kind = ReadableAiActionKind.ShowSearchResults,
                Title = "搜索相关接口",
                Summary = $"在 {context.Collections.Count} 个 collection 和 {context.Specifications.Count} 个 specification 中搜索相关接口。"
            }));
            return Task.FromResult(result);
        }

        result.AssistantMessage = BuildContextMessage(context);
        result.Findings.Add(new ReadableAiFinding
        {
            Title = "Available tools",
            Detail = string.Join(", ", _tools.Tools.Select(tool => tool.Name)),
            Severity = "info"
        });
        return Task.FromResult(result);
    }

    private void AddPatchAction(ReadableAiTurnResult result, ReadableAiRequestPatch patch)
    {
        result.Actions.Add(ApplyPolicy(new ReadableAiAction
        {
            Kind = ReadableAiActionKind.UpdateCurrentRequest,
            Title = "应用到当前请求",
            Summary = "将 AI 生成的参数草稿应用到当前请求模型。",
            RequestPatch = patch
        }));
    }

    private ReadableAiAction ApplyPolicy(ReadableAiAction action)
    {
        action.Confirmation = _confirmationPolicy.Evaluate(action);
        return action;
    }

    private static ReadableAiRequestPatch BuildParameterPatch(ReadableAiWorkspaceContext context)
    {
        var request = context.CurrentRequest;
        var patch = new ReadableAiRequestPatch();
        if (request is null)
        {
            return patch;
        }

        if (!request.Headers.Any(header => header.Name.Equals("Accept", StringComparison.OrdinalIgnoreCase)))
        {
            patch.HeaderChanges.Add(new ReadableAiParameterChange
            {
                Location = "headers",
                Name = "Accept",
                CurrentValue = null,
                SuggestedValue = "application/json",
                Reason = "Most API requests in this client expect structured JSON responses."
            });
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            || request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Headers.Any(header => header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
            {
                patch.HeaderChanges.Add(new ReadableAiParameterChange
                {
                    Location = "headers",
                    Name = "Content-Type",
                    CurrentValue = null,
                    SuggestedValue = "application/json",
                    Reason = "The request has a body-capable method and likely needs a JSON content type."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Body?.Content))
            {
                patch.BodyType = "Json";
                patch.BodyContentType = "application/json";
                patch.BodyText = "{\n  \"name\": \"value\"\n}";
            }
        }

        if (!request.Query.Any() && Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && string.IsNullOrWhiteSpace(uri.Query))
        {
            patch.QueryChanges.Add(new ReadableAiParameterChange
            {
                Location = "query",
                Name = "page",
                CurrentValue = null,
                SuggestedValue = "1",
                Reason = "A common paging parameter; review before applying if this endpoint is not paginated."
            });
        }

        return patch;
    }

    private static string BuildParameterMessage(ReadableAiWorkspaceContext context)
    {
        if (context.CurrentRequest is null)
        {
            return "当前没有可编辑的请求。我可以在你打开一个请求后生成 query、headers 或 body 草稿。";
        }

        return $"我基于当前 {context.CurrentRequest.Method} 请求生成了一个参数草稿。变更需要你确认后才会写入请求模型。";
    }

    private static string BuildHistoryMessage(ReadableAiWorkspaceContext context)
    {
        if (context.RecentExchanges.Count == 0)
        {
            return "当前还没有可对比的消息记录。后续接入执行历史存储后，我会读取最近三次 request/response 快照并总结差异。";
        }

        return $"找到 {context.RecentExchanges.Count} 条相关消息记录。我会优先比较状态码、耗时、headers、query 和 body 差异。";
    }

    private static string BuildResponseExplanation(ReadableAiWorkspaceContext context)
    {
        var exchange = context.CurrentExchange ?? context.RecentExchanges.FirstOrDefault();
        if (exchange?.Response is null)
        {
            return "当前还没有响应结果。先发送请求后，我可以读取 response、状态码和请求参数来分析失败原因。";
        }

        var request = exchange.Request ?? context.CurrentRequest;
        var statusCode = exchange.Response.StatusCode;
        var body = exchange.Response.BodyText ?? string.Empty;
        var statusSummary = statusCode switch
        {
            >= 500 => "服务端错误，优先检查接口日志、上游依赖和请求 body 是否满足后端约束。",
            >= 400 => "客户端请求可能不符合接口要求，优先检查认证、路径参数、query、headers 和 body。",
            >= 300 => "响应是重定向，检查 Location、代理配置以及是否需要跟随跳转。",
            >= 200 => "请求已成功返回，可以继续检查响应字段是否符合预期。",
            _ => "没有明确 HTTP 状态码，可能是网络异常、取消或本地执行失败。"
        };

        var bodyHint = string.IsNullOrWhiteSpace(body)
            ? "响应体为空。"
            : $"响应体约 {body.Length} 个字符，我会优先关注 error/message/code/detail 等字段。";
        var requestHint = request is null ? "当前没有请求快照。" : $"当前请求是 {request.Method} {request.Url}。";

        return $"{requestHint}\n{statusSummary}\n{bodyHint}";
    }

    private static string BuildSearchMessage(ReadableAiWorkspaceContext context, string prompt)
    {
        var candidates = context.Collections
            .SelectMany(collection => collection.Requests.Select(request => $"{request.Method} {request.Url} ({collection.Name}/{request.Name})"))
            .Where(value => PromptMatches(value, prompt))
            .Take(5)
            .ToList();

        if (candidates.Count == 0)
        {
            return $"我已经准备按 “{prompt}” 搜索接口。当前没有直接命中的请求，后续可以接入 spec operation 索引和语义检索增强结果。";
        }

        return "找到一些可能相关的接口：\n" + string.Join("\n", candidates.Select(item => $"- {item}"));
    }

    private static string BuildContextMessage(ReadableAiWorkspaceContext context)
    {
        if (context.CurrentRequest is null)
        {
            return $"当前 workspace 是 {context.WorkspaceName}。我可以帮你搜索接口、分析 spec，或在打开请求后生成参数。";
        }

        return $"当前请求是 {context.CurrentRequest.Method} {context.CurrentRequest.Url}。你可以让我生成参数、解释响应、搜索相关接口或对比历史记录。";
    }

    private static bool PromptMatches(string value, string prompt)
    {
        return prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
