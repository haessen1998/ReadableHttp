using ReadableHttp;

internal static class ReadableCliOptions
{
    public static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static IEnumerable<string> ReadOptions(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                yield return args[i + 1];
            }
        }
    }

    public static bool HasOption(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    public static (string Name, string Value) SplitAssignment(string assignment, string optionName)
    {
        var separator = assignment.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new ArgumentException($"{optionName} requires NAME=VALUE.");
        }

        return (assignment[..separator], assignment[(separator + 1)..]);
    }

    public static ReadableStreamFormat ReadStreamFormat(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" or "auto" => ReadableStreamFormat.Auto,
            "sse" or "server-sent-events" => ReadableStreamFormat.ServerSentEvents,
            "lines" or "line" or "jsonl" or "ndjson" => ReadableStreamFormat.Lines,
            "raw" => ReadableStreamFormat.Raw,
            _ => throw new ArgumentException($"Unknown stream format '{value}'.")
        };
    }
}
