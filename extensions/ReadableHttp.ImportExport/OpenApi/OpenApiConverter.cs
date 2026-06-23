using System.Text.Json;
using System.Text.Json.Nodes;
using ReadableHttp;
using ReadableHttp.ImportExport.OpenApi;

namespace ReadableHttp.ImportExport;

public sealed class OpenApiConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<ReadableRequest> Import(string content, string extension)
    {
        var document = OpenApiRequestFactory.ParseDocument(content, extension);
        return new OpenApiRequestFactory()
            .CreateRequests(document)
            .Select(operation => operation.Request)
            .ToList();
    }

    public string Export(IEnumerable<ReadableRequest> requests, string title = "ReadableHttp API", string version = "1.0.0")
    {
        var paths = new JsonObject();
        foreach (var request in requests)
        {
            var (path, query) = SplitPathAndQuery(request.Url);
            var pathItem = paths[path]?.AsObject() ?? [];
            paths[path] = pathItem;

            var operation = new JsonObject
            {
                ["operationId"] = ToOperationId(request),
                ["summary"] = request.Name,
                ["parameters"] = CreateParameters(request, query),
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject
                    {
                        ["description"] = "OK"
                    }
                }
            };

            if (request.Body is not null && request.Body.Type != ReadableBodyType.None)
            {
                operation["requestBody"] = CreateRequestBody(request.Body);
            }

            pathItem[request.Method.ToLowerInvariant()] = operation;
        }

        var document = new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = new JsonObject
            {
                ["title"] = title,
                ["version"] = version
            },
            ["paths"] = paths
        };

        return document.ToJsonString(JsonOptions);
    }

    private static JsonArray CreateParameters(ReadableRequest request, IReadOnlyDictionary<string, string?> query)
    {
        var parameters = new JsonArray();

        foreach (var item in query)
        {
            parameters.Add(CreateParameter(item.Key, "query", item.Value));
        }

        foreach (var item in request.Query.Where(item => item.Enabled))
        {
            parameters.Add(CreateParameter(item.Name, "query", item.Value));
        }

        foreach (var header in request.Headers.Where(header => header.Enabled))
        {
            if (string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header.Name, "accept", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            parameters.Add(CreateParameter(header.Name, "header", header.Value));
        }

        return parameters;
    }

    private static JsonObject CreateParameter(string name, string location, string? value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["in"] = location,
            ["required"] = location == "path",
            ["schema"] = new JsonObject
            {
                ["type"] = "string",
                ["example"] = value
            }
        };
    }

    private static JsonObject CreateRequestBody(ReadableBody body)
    {
        var contentType = body.ContentType ?? body.Type switch
        {
            ReadableBodyType.Json => "application/json",
            ReadableBodyType.Xml => "application/xml",
            ReadableBodyType.FormUrlEncoded => "application/x-www-form-urlencoded",
            _ => "text/plain"
        };

        return new JsonObject
        {
            ["content"] = new JsonObject
            {
                [contentType] = new JsonObject
                {
                    ["example"] = body.Type == ReadableBodyType.FormUrlEncoded
                        ? string.Join("&", body.Form.Where(item => item.Enabled).Select(item => $"{item.Name}={item.Value}"))
                        : body.Content
                }
            }
        };
    }

    private static (string Path, IReadOnlyDictionary<string, string?> Query) SplitPathAndQuery(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && !Uri.TryCreate(url, UriKind.Relative, out _))
        {
            return (url, new Dictionary<string, string?>());
        }

        var path = absolute?.AbsolutePath ?? url.Split('?', 2)[0];
        var queryText = absolute?.Query.TrimStart('?') ?? (url.Contains('?', StringComparison.Ordinal) ? url.Split('?', 2)[1] : string.Empty);
        var query = queryText
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => Uri.UnescapeDataString(part[0]), part => part.Length > 1 ? Uri.UnescapeDataString(part[1]) : null);

        return (string.IsNullOrWhiteSpace(path) ? "/" : path, query);
    }

    private static string ToOperationId(ReadableRequest request)
    {
        var name = new string(request.Name
            .Where(character => char.IsLetterOrDigit(character) || character == ' ')
            .ToArray());
        var pascal = string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        return string.IsNullOrWhiteSpace(pascal)
            ? $"{request.Method.ToLowerInvariant()}Request"
            : $"{request.Method.ToLowerInvariant()}{pascal}";
    }
}
