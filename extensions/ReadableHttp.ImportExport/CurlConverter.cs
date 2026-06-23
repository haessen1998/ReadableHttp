using System.Text;
using ReadableHttp.Core;

namespace ReadableHttp.ImportExport;

public sealed class CurlConverter
{
    public ReadableRequest Import(string command)
    {
        var tokens = Tokenize(command);
        if (tokens.Count == 0 || !string.Equals(tokens[0], "curl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("curl command must start with curl.", nameof(command));
        }

        var request = new ReadableRequest { Name = "curl import", Method = "GET" };

        for (var i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if ((token == "-X" || token == "--request") && i + 1 < tokens.Count)
            {
                request.Method = tokens[++i].ToUpperInvariant();
            }
            else if ((token == "-H" || token == "--header") && i + 1 < tokens.Count)
            {
                AddHeader(request, tokens[++i]);
            }
            else if ((token == "-d" || token == "--data" || token == "--data-raw") && i + 1 < tokens.Count)
            {
                request.Method = request.Method == "GET" ? "POST" : request.Method;
                request.Body = new ReadableBody
                {
                    Type = ReadableBodyType.Raw,
                    Content = tokens[++i],
                    ContentType = request.Headers.FirstOrDefault(header =>
                        string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase))?.Value ?? "text/plain"
                };
            }
            else if (!token.StartsWith('-') && string.IsNullOrWhiteSpace(request.Url))
            {
                request.Url = token;
            }
        }

        return request;
    }

    public string Export(ReadableRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("curl");
        builder.Append(" -X ").Append(Escape(request.Method));
        builder.Append(' ').Append(Escape(BuildUrl(request)));

        foreach (var header in request.Headers.Where(header => header.Enabled))
        {
            builder.Append(" -H ").Append(Escape($"{header.Name}: {header.Value}"));
        }

        if (request.Body is not null && request.Body.Type != ReadableBodyType.None)
        {
            builder.Append(" --data-raw ").Append(Escape(request.Body.Content ?? string.Empty));
        }

        return builder.ToString();
    }

    private static void AddHeader(ReadableRequest request, string header)
    {
        var separator = header.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return;
        }

        request.Headers.Add(new ReadableNameValue
        {
            Name = header[..separator].Trim(),
            Value = header[(separator + 1)..].Trim()
        });
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

    private static string Escape(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static List<string> Tokenize(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';

        foreach (var character in command)
        {
            if (quote == '\0' && char.IsWhiteSpace(character))
            {
                AddToken(tokens, current);
                continue;
            }

            if ((character == '\'' || character == '"') && quote == '\0')
            {
                quote = character;
                continue;
            }

            if (character == quote)
            {
                quote = '\0';
                continue;
            }

            current.Append(character);
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }
}
