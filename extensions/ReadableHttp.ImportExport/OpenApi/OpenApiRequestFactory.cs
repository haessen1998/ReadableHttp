using System.Text.Json;
using System.Text.Json.Nodes;
using ReadableHttp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReadableHttp.ImportExport.OpenApi;

public sealed class OpenApiRequestFactory
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get",
        "post",
        "put",
        "patch",
        "delete",
        "head",
        "options",
        "trace"
    };

    public async Task<ReadableRequest> CreateRequestAsync(
        string specPath,
        string operation,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(specPath, cancellationToken);
        var document = ParseDocument(content, Path.GetExtension(specPath));
        return CreateRequest(document, operation);
    }

    public IReadOnlyList<OpenApiReadableOperation> CreateRequests(JsonNode document)
    {
        var requests = new List<OpenApiReadableOperation>();
        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI document does not contain paths.");

        foreach (var path in paths)
        {
            var pathItem = path.Value?.AsObject();
            if (pathItem is null)
            {
                continue;
            }

            foreach (var method in pathItem)
            {
                if (!HttpMethods.Contains(method.Key) || method.Value is null)
                {
                    continue;
                }

                var request = CreateRequest(document, $"{method.Key} {path.Key}");
                requests.Add(new OpenApiReadableOperation
                {
                    OperationId = method.Value["operationId"]?.GetValue<string>(),
                    Path = path.Key,
                    Method = method.Key.ToUpperInvariant(),
                    Summary = method.Value["summary"]?.GetValue<string>(),
                    Tags = method.Value["tags"] is JsonArray tags
                        ? tags.Select(tag => tag?.GetValue<string>()).Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag!).ToList()
                        : [],
                    Request = request
                });
            }
        }

        return requests;
    }

    public ReadableRequest CreateRequest(JsonNode document, string operation)
    {
        var match = FindOperation(document, operation);
        var serverUrl = GetServerUrl(document);
        var url = $"{serverUrl}{match.Path}";

        var request = new ReadableRequest
        {
            Name = match.Operation["summary"]?.GetValue<string>()
                ?? match.Operation["operationId"]?.GetValue<string>()
                ?? $"{match.Method.ToUpperInvariant()} {match.Path}",
            Method = match.Method.ToUpperInvariant(),
            Url = url
        };

        AddParameters(document, request, match.PathItem["parameters"]);
        AddParameters(document, request, match.Operation["parameters"]);
        AddRequestBody(document, request, match.Operation["requestBody"]);
        AddSwagger2Body(document, request, match.Operation["parameters"]);
        ApplySecurity(document, request, match.Operation["security"] ?? document["security"]);

        return request;
    }

    public static JsonNode ParseDocument(string content, string extension)
    {
        if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = deserializer.Deserialize<object>(content);
            var json = JsonSerializer.Serialize(yaml);
            return JsonNode.Parse(json) ?? throw new InvalidOperationException("OpenAPI YAML document is empty.");
        }

        return JsonNode.Parse(content) ?? throw new InvalidOperationException("OpenAPI JSON document is empty.");
    }

    private static OpenApiOperationMatch FindOperation(JsonNode document, string operation)
    {
        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI document does not contain paths.");

        foreach (var path in paths)
        {
            var pathItem = path.Value?.AsObject();
            if (pathItem is null)
            {
                continue;
            }

            foreach (var method in pathItem)
            {
                if (!HttpMethods.Contains(method.Key) || method.Value is null)
                {
                    continue;
                }

                var operationNode = method.Value;
                var operationId = operationNode["operationId"]?.GetValue<string>();
                if (string.Equals(operationId, operation, StringComparison.OrdinalIgnoreCase)
                    || string.Equals($"{method.Key} {path.Key}", operation, StringComparison.OrdinalIgnoreCase))
                {
                    return new OpenApiOperationMatch(path.Key, method.Key, pathItem, operationNode);
                }
            }
        }

        throw new InvalidOperationException($"OpenAPI operation '{operation}' was not found.");
    }

    private static string GetServerUrl(JsonNode document)
    {
        var openApiServer = document["servers"]?[0]?["url"]?.GetValue<string>()?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(openApiServer))
        {
            return openApiServer;
        }

        var host = document["host"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var scheme = document["schemes"] is JsonArray schemes && schemes.Count > 0
            ? schemes[0]?.GetValue<string>()
            : "https";
        var basePath = document["basePath"]?.GetValue<string>() ?? string.Empty;
        return $"{scheme}://{host}{basePath}".TrimEnd('/');
    }

    private static void AddParameters(JsonNode document, ReadableRequest request, JsonNode? parameters)
    {
        if (parameters is not JsonArray array)
        {
            return;
        }

        foreach (var parameter in array)
        {
            var resolved = ResolveReference(document, parameter);
            var name = resolved?["name"]?.GetValue<string>();
            var location = resolved?["in"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var value = "{{" + name + "}}";
            if (string.Equals(location, "query", StringComparison.OrdinalIgnoreCase))
            {
                request.Query.Add(new ReadableNameValue { Name = name, Value = value });
            }
            else if (string.Equals(location, "path", StringComparison.OrdinalIgnoreCase))
            {
                request.PathParameters.Add(new ReadableNameValue { Name = name, Value = value });
            }
            else if (string.Equals(location, "header", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Add(new ReadableNameValue { Name = name, Value = value });
            }
        }
    }

    private static void ApplySecurity(JsonNode document, ReadableRequest request, JsonNode? security)
    {
        if (security is not JsonArray securityArray || securityArray.Count == 0)
        {
            return;
        }

        var schemes = document["components"]?["securitySchemes"]?.AsObject()
            ?? document["securityDefinitions"]?.AsObject();
        if (schemes is null)
        {
            return;
        }

        var firstRequirement = securityArray[0]?.AsObject();
        var schemeName = firstRequirement?.FirstOrDefault().Key;
        if (string.IsNullOrWhiteSpace(schemeName) || schemes[schemeName] is null)
        {
            return;
        }

        var scheme = ResolveReference(document, schemes[schemeName]) ?? schemes[schemeName]!;
        var type = scheme["type"]?.GetValue<string>();
        if (string.Equals(type, "http", StringComparison.OrdinalIgnoreCase)
            && string.Equals(scheme["scheme"]?.GetValue<string>(), "bearer", StringComparison.OrdinalIgnoreCase))
        {
            request.Auth = new ReadableAuth
            {
                Type = ReadableAuthType.Bearer,
                Token = "{{token}}"
            };
        }
        else if (string.Equals(type, "apiKey", StringComparison.OrdinalIgnoreCase))
        {
            request.Auth = new ReadableAuth
            {
                Type = ReadableAuthType.ApiKey,
                Name = scheme["name"]?.GetValue<string>() ?? schemeName,
                Value = "{{apiKey}}",
                ApiKeyLocation = string.Equals(scheme["in"]?.GetValue<string>(), "query", StringComparison.OrdinalIgnoreCase)
                    ? ReadableApiKeyLocation.Query
                    : ReadableApiKeyLocation.Header
            };
        }
        else if (string.Equals(type, "oauth2", StringComparison.OrdinalIgnoreCase))
        {
            request.Auth = new ReadableAuth
            {
                Type = ReadableAuthType.OAuth2,
                OAuth2 = new ReadableOAuth2Options
                {
                    TokenPlacement = ReadableTokenPlacement.Header,
                    TokenName = "Authorization",
                    HeaderPrefix = "Bearer",
                    ExtraParameters =
                    {
                        ["credentials"] = "{{accessToken}}"
                    }
                }
            };
        }
    }

    private static void AddRequestBody(JsonNode document, ReadableRequest request, JsonNode? requestBody)
    {
        requestBody = ResolveReference(document, requestBody);
        var content = requestBody?["content"]?.AsObject();
        if (content is null)
        {
            return;
        }

        if (content.TryGetPropertyValue("application/json", out var jsonContent))
        {
            request.Body = new ReadableBody
            {
                Type = ReadableBodyType.Json,
                ContentType = "application/json",
                Content = CreateExampleContent(document, jsonContent) ?? "{\n}"
            };
            return;
        }

        if (content.TryGetPropertyValue("application/x-www-form-urlencoded", out var formContent))
        {
            request.Body = new ReadableBody
            {
                Type = ReadableBodyType.FormUrlEncoded,
                Form = CreateFormFields(document, formContent)
            };
            return;
        }

        if (content.TryGetPropertyValue("multipart/form-data", out var multipartContent))
        {
            request.Body = new ReadableBody
            {
                Type = ReadableBodyType.MultipartFormData,
                Multipart = CreateMultipartFields(document, multipartContent)
            };
            return;
        }

        var first = content.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(first.Key))
        {
            request.Body = new ReadableBody
            {
                Type = ReadableBodyType.Raw,
                ContentType = first.Key,
                Content = CreateExampleContent(document, first.Value) ?? string.Empty
            };
        }
    }

    private static void AddSwagger2Body(JsonNode document, ReadableRequest request, JsonNode? parameters)
    {
        if (request.Body is not null || parameters is not JsonArray array)
        {
            return;
        }

        var bodyParameter = array
            .Select(parameter => ResolveReference(document, parameter))
            .FirstOrDefault(parameter => string.Equals(parameter?["in"]?.GetValue<string>(), "body", StringComparison.OrdinalIgnoreCase));
        if (bodyParameter is not null)
        {
            var schema = ResolveReference(document, bodyParameter["schema"]);
            var example = CreateExampleFromSchema(document, schema);
            request.Body = new ReadableBody
            {
                Type = ReadableBodyType.Json,
                ContentType = "application/json",
                Content = example?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{\n}"
            };
            return;
        }

        var formParameters = array
            .Select(parameter => ResolveReference(document, parameter))
            .Where(parameter => string.Equals(parameter?["in"]?.GetValue<string>(), "formData", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (formParameters.Count == 0)
        {
            return;
        }

        var hasFile = formParameters.Any(parameter => string.Equals(parameter?["type"]?.GetValue<string>(), "file", StringComparison.OrdinalIgnoreCase));
        request.Body = new ReadableBody
        {
            Type = hasFile ? ReadableBodyType.MultipartFormData : ReadableBodyType.FormUrlEncoded,
            Form = hasFile
                ? []
                : formParameters.Select(parameter => new ReadableNameValue
                {
                    Name = parameter?["name"]?.GetValue<string>() ?? string.Empty,
                    Value = "{{" + (parameter?["name"]?.GetValue<string>() ?? "value") + "}}"
                }).ToList(),
            Multipart = hasFile
                ? formParameters.Select(parameter => new ReadableMultipartItem
                {
                    Name = parameter?["name"]?.GetValue<string>() ?? string.Empty,
                    Type = string.Equals(parameter?["type"]?.GetValue<string>(), "file", StringComparison.OrdinalIgnoreCase)
                        ? ReadableMultipartItemType.File
                        : ReadableMultipartItemType.Text,
                    Value = string.Equals(parameter?["type"]?.GetValue<string>(), "file", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : "{{" + (parameter?["name"]?.GetValue<string>() ?? "value") + "}}",
                    FilePath = string.Equals(parameter?["type"]?.GetValue<string>(), "file", StringComparison.OrdinalIgnoreCase)
                        ? "{{" + (parameter?["name"]?.GetValue<string>() ?? "file") + "Path}}"
                        : null
                }).ToList()
                : []
        };
    }

    private static string? CreateExampleContent(JsonNode document, JsonNode? mediaType)
    {
        var example = mediaType?["example"];
        if (example is not null)
        {
            return example.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        var schema = mediaType?["schema"];
        var generated = CreateExampleFromSchema(document, schema);
        return generated?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode? CreateExampleFromSchema(JsonNode document, JsonNode? schema)
    {
        schema = ResolveReference(document, schema);
        if (schema?["example"] is not null)
        {
            return schema["example"]!.DeepClone();
        }

        if (schema?["default"] is not null)
        {
            return schema["default"]!.DeepClone();
        }

        var type = schema?["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type) && schema?["properties"] is JsonObject)
        {
            type = "object";
        }

        return type switch
        {
            "object" => CreateObjectExample(document, schema),
            "array" => new JsonArray(CreateExampleFromSchema(document, schema?["items"])),
            "integer" => 0,
            "number" => 0,
            "boolean" => true,
            "string" => "string",
            _ => null
        };
    }

    private static JsonObject CreateObjectExample(JsonNode document, JsonNode? schema)
    {
        var result = new JsonObject();
        if (schema?["properties"] is not JsonObject properties)
        {
            return result;
        }

        foreach (var property in properties)
        {
            result[property.Key] = CreateExampleFromSchema(document, property.Value) ?? "string";
        }

        return result;
    }

    private static List<ReadableNameValue> CreateFormFields(JsonNode document, JsonNode? mediaType)
    {
        var fields = new List<ReadableNameValue>();
        var schema = ResolveReference(document, mediaType?["schema"]);
        if (schema?["properties"] is not JsonObject properties)
        {
            return fields;
        }

        foreach (var property in properties)
        {
            fields.Add(new ReadableNameValue
            {
                Name = property.Key,
                Value = "{{" + property.Key + "}}"
            });
        }

        return fields;
    }

    private static List<ReadableMultipartItem> CreateMultipartFields(JsonNode document, JsonNode? mediaType)
    {
        var fields = new List<ReadableMultipartItem>();
        var schema = ResolveReference(document, mediaType?["schema"]);
        if (schema?["properties"] is not JsonObject properties)
        {
            return fields;
        }

        foreach (var property in properties)
        {
            var resolved = ResolveReference(document, property.Value);
            var format = resolved?["format"]?.GetValue<string>();
            var type = string.Equals(format, "binary", StringComparison.OrdinalIgnoreCase)
                ? ReadableMultipartItemType.File
                : ReadableMultipartItemType.Text;
            fields.Add(new ReadableMultipartItem
            {
                Name = property.Key,
                Type = type,
                Value = type == ReadableMultipartItemType.Text ? "{{" + property.Key + "}}" : null,
                FilePath = type == ReadableMultipartItemType.File ? "{{" + property.Key + "Path}}" : null
            });
        }

        return fields;
    }

    private static JsonNode? ResolveReference(JsonNode document, JsonNode? node)
    {
        var reference = node?["$ref"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return node;
        }

        JsonNode? current = document;
        foreach (var rawSegment in reference[2..].Split('/'))
        {
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            current = current?[segment];
            if (current is null)
            {
                return node;
            }
        }

        return current;
    }

    private sealed record OpenApiOperationMatch(
        string Path,
        string Method,
        JsonObject PathItem,
        JsonNode Operation);
}

public sealed class OpenApiReadableOperation
{
    public string? OperationId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string? Summary { get; set; }

    public List<string> Tags { get; set; } = [];

    public ReadableRequest Request { get; set; } = new();
}
