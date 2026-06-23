using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ReadableHttp;

namespace ReadableHttp.Execution;

internal static class ReadableRequestVariableResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static ReadableRequest Resolve(ReadableRequest request, ReadableExecutionContext context)
    {
        var variables = new Dictionary<string, ReadableVariable>(context.Variables, StringComparer.OrdinalIgnoreCase);
        foreach (var variable in request.Variables)
        {
            variables[variable.Key] = variable.Value;
        }

        var node = JsonSerializer.SerializeToNode(request, JsonOptions);
        ReplaceStringValues(node, variables);
        return node.Deserialize<ReadableRequest>(JsonOptions) ?? new ReadableRequest();
    }

    private static void ReplaceStringValues(JsonNode? node, IReadOnlyDictionary<string, ReadableVariable> variables)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                var isJsonBody = IsJsonBody(jsonObject);
                foreach (var property in jsonObject.ToArray())
                {
                    if (isJsonBody && string.Equals(property.Key, "content", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (property.Value is JsonValue jsonValue
                        && jsonValue.TryGetValue<string>(out var text))
                    {
                        jsonObject[property.Key] = ReplaceVariables(text, variables);
                    }
                    else
                    {
                        ReplaceStringValues(property.Value, variables);
                    }
                }

                if (isJsonBody
                    && jsonObject["content"] is JsonValue contentValue
                    && contentValue.TryGetValue<string>(out var content))
                {
                    jsonObject["content"] = ReplaceJsonBodyContent(content, variables);
                }

                break;

            case JsonArray jsonArray:
                for (var index = 0; index < jsonArray.Count; index++)
                {
                    if (jsonArray[index] is JsonValue jsonValue
                        && jsonValue.TryGetValue<string>(out var text))
                    {
                        jsonArray[index] = ReplaceVariables(text, variables);
                    }
                    else
                    {
                        ReplaceStringValues(jsonArray[index], variables);
                    }
                }

                break;
        }
    }

    private static string ReplaceVariables(string value, IReadOnlyDictionary<string, ReadableVariable> variables)
    {
        foreach (var (name, variable) in variables)
        {
            value = value.Replace("{{" + name + "}}", variable.ToTemplateValue() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static bool IsJsonBody(JsonObject jsonObject)
    {
        return jsonObject["type"] is JsonValue typeValue
            && typeValue.TryGetValue<string>(out var type)
            && string.Equals(type, "json", StringComparison.OrdinalIgnoreCase)
            && jsonObject.ContainsKey("content");
    }

    private static string ReplaceJsonBodyContent(string content, IReadOnlyDictionary<string, ReadableVariable> variables)
    {
        try
        {
            var node = JsonNode.Parse(content);
            ReplaceStringValues(node, variables);
            return node?.ToJsonString() ?? string.Empty;
        }
        catch (JsonException)
        {
            return ReplaceVariables(content, variables);
        }
    }
}
