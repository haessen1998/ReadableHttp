using System.Text;
using ReadableHttp;

namespace ReadableHttp.ImportExport;

public sealed class HttpFileConverter
{
    public IReadOnlyList<ReadableRequest> Import(string content)
    {
        var variables = ParseVariables(content);
        var requests = new List<ReadableRequest>();
        foreach (var block in SplitBlocks(content))
        {
            var request = ParseBlock(block);
            if (request is not null)
            {
                foreach (var variable in variables)
                {
                    request.Variables.TryAdd(variable.Key, variable.Value);
                }

                requests.Add(request);
            }
        }

        return requests;
    }

    public string Export(IEnumerable<ReadableRequest> requests)
    {
        var builder = new StringBuilder();
        foreach (var request in requests)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"### {request.Name}");
            builder.AppendLine($"{request.Method} {BuildUrl(request)}");

            foreach (var header in request.Headers.Where(header => header.Enabled))
            {
                builder.AppendLine($"{header.Name}: {header.Value}");
            }

            if (request.Body is not null && request.Body.Type != ReadableBodyType.None)
            {
                builder.AppendLine();
                builder.AppendLine(request.Body.Type == ReadableBodyType.FormUrlEncoded
                    ? string.Join("&", request.Body.Form.Where(item => item.Enabled).Select(item => $"{item.Name}={item.Value}"))
                    : request.Body.Content);
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SplitBlocks(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var blocks = normalized.Split("\n###", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var block in blocks)
        {
            yield return block.StartsWith("###", StringComparison.Ordinal) ? block[3..] : block;
        }
    }

    private static ReadableRequest? ParseBlock(string block)
    {
        var lines = block.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var index = 0;
        var name = "HTTP Request";

        if (index < lines.Length)
        {
            lines[index] = lines[index].TrimStart('#').TrimStart();
        }

        if (index < lines.Length && !LooksLikeRequestLine(lines[index]))
        {
            name = lines[index].Trim().TrimStart('#').Trim();
            index++;
        }

        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        if (index >= lines.Length || !LooksLikeRequestLine(lines[index]))
        {
            return null;
        }

        var requestLine = lines[index++].Trim();
        var requestLineParts = requestLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var request = new ReadableRequest
        {
            Name = string.IsNullOrWhiteSpace(name) ? requestLine : name,
            Method = requestLineParts[0],
            Url = requestLineParts.Length > 1 ? requestLineParts[1] : string.Empty
        };

        while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
        {
            var separator = lines[index].IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
            {
                request.Headers.Add(new ReadableNameValue
                {
                    Name = lines[index][..separator].Trim(),
                    Value = lines[index][(separator + 1)..].Trim(),
                    Enabled = true
                });
            }

            index++;
        }

        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        if (index < lines.Length)
        {
            var body = string.Join('\n', lines[index..]).TrimEnd();
            var bodyType = DetectBodyType(request.Headers);
            request.Body = new ReadableBody
            {
                Type = bodyType,
                ContentType = request.Headers.FirstOrDefault(header =>
                    string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase))?.Value,
                Content = body
            };

            if (bodyType == ReadableBodyType.FormUrlEncoded)
            {
                request.Body.Form = ParseForm(body);
            }
            else if (bodyType == ReadableBodyType.MultipartFormData)
            {
                request.Body.Multipart = ParseMultipart(body);
            }
        }

        return request;
    }

    private static bool LooksLikeRequestLine(string line)
    {
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var method = parts[0];
        var target = parts[1];
        var isMethod = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);

        return isMethod
            && (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("/", StringComparison.Ordinal)
                || target.StartsWith("{{", StringComparison.Ordinal));
    }

    private static ReadableBodyType DetectBodyType(IEnumerable<ReadableNameValue> headers)
    {
        var contentType = headers.FirstOrDefault(header =>
            string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase))?.Value;

        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ReadableBodyType.Json;
        }

        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ReadableBodyType.Xml;
        }

        if (contentType?.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ReadableBodyType.FormUrlEncoded;
        }

        if (contentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ReadableBodyType.MultipartFormData;
        }

        return ReadableBodyType.Raw;
    }

    private static string BuildUrl(ReadableRequest request)
    {
        if (request.Query.Count == 0)
        {
            return request.Url;
        }

        var query = string.Join("&", request.Query
            .Where(item => item.Enabled)
            .Select(item => $"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(item.Value ?? string.Empty)}"));
        var separator = request.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{request.Url}{separator}{query}";
    }

    private static Dictionary<string, string?> ParseVariables(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('@') && line.Contains('=', StringComparison.Ordinal))
            .Select(line => line[1..].Split('=', 2))
            .ToDictionary(part => part[0].Trim(), part => (string?)part[1].Trim());
    }

    private static List<ReadableNameValue> ParseForm(string body)
    {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Select(part => new ReadableNameValue
            {
                Name = Uri.UnescapeDataString(part[0]),
                Value = part.Length > 1 ? Uri.UnescapeDataString(part[1]) : string.Empty
            })
            .ToList();
    }

    private static List<ReadableMultipartItem> ParseMultipart(string body)
    {
        return body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains('=', StringComparison.Ordinal))
            .Select(line =>
            {
                var part = line.Split('=', 2);
                var value = part[1];
                if (value.StartsWith('@'))
                {
                    var segments = value[1..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return new ReadableMultipartItem
                    {
                        Name = part[0],
                        Type = ReadableMultipartItemType.File,
                        FilePath = segments[0],
                        ContentType = segments.FirstOrDefault(segment => segment.StartsWith("type=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1]
                    };
                }

                return new ReadableMultipartItem
                {
                    Name = part[0],
                    Value = value
                };
            })
            .ToList();
    }
}
