using System.Text.Json;
using ReadableHttp;
using ReadableHttp.Execution;
using ReadableHttp.ImportExport;
using ReadableHttp.ImportExport.OpenApi;
using ReadableHttp.Storage;
using ReadableHttp.Try;

var exitCode = await RunAsync(args);
return exitCode;

static async Task<int> RunAsync(string[] args)
{
    if (args.Length < 1)
    {
        WriteUsage();
        return 1;
    }

    return args[0].ToLowerInvariant() switch
    {
        "send" => await SendAsync(args[1..]),
        "stream" => await StreamAsync(args[1..]),
        "try" => await TryOpenApiAsync(args[1..]),
        "trydoc" => await TryDocumentAsync(args[1..]),
        "import" => await ImportAsync(args[1..]),
        "export" => await ExportAsync(args[1..]),
        "init" => await InitAsync(args[1..]),
        _ => WriteUnknownCommand(args[0])
    };
}

static async Task<int> SendAsync(string[] args)
{
    if (args.Length < 1 && string.IsNullOrWhiteSpace(ReadOption(args, "--workspace")))
    {
        WriteUsage();
        return 1;
    }

    var storage = new ReadableHttpJsonStorage();
    var request = await LoadRequestAsync(storage, args);
    var context = await CreateContextAsync(storage, args);
    ApplyRequestOverrides(request, args);

    var exchange = await new ReadableHttpExecutor().SendAsync(request, context);
    Console.WriteLine($"HTTP {(exchange.Response?.StatusCode.ToString() ?? "ERROR")} {exchange.Response?.ReasonPhrase}");

    if (exchange.Error is not null)
    {
        Console.Error.WriteLine($"{exchange.Error.Type}: {exchange.Error.Message}");
    }
    else if (!string.IsNullOrEmpty(exchange.Response?.BodyText))
    {
        Console.WriteLine(exchange.Response.BodyText);
    }

    var outputPath = ReadOption(args, "--output");
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        await storage.SaveAsync(outputPath, exchange);
    }

    if (exchange.Error is not null)
    {
        return 2;
    }

    return exchange.Response?.StatusCode is >= 200 and < 400 ? 0 : 3;
}

static async Task<int> StreamAsync(string[] args)
{
    if (args.Length < 1 && string.IsNullOrWhiteSpace(ReadOption(args, "--workspace")))
    {
        WriteUsage();
        return 1;
    }

    var storage = new ReadableHttpJsonStorage();
    var request = await LoadRequestAsync(storage, args);
    var context = await CreateContextAsync(storage, args);
    ApplyRequestOverrides(request, args);

    var format = ReadStreamFormat(ReadOption(args, "--format"));
    var executor = new ReadableHttpExecutor();

    await foreach (var message in executor.StreamAsync(
        request,
        context,
        new ReadableStreamOptions { Format = format }))
    {
        switch (message.Type)
        {
            case ReadableStreamMessageType.Headers:
                Console.WriteLine($"HTTP {message.StatusCode} {message.ReasonPhrase}");
                break;
            case ReadableStreamMessageType.Data:
                Console.WriteLine(message.Data ?? message.Raw);
                break;
            case ReadableStreamMessageType.Error:
                Console.Error.WriteLine($"{message.Error?.Type}: {message.Error?.Message}");
                return 2;
        }
    }

    return 0;
}

static async Task<int> TryOpenApiAsync(string[] args)
{
    if (args.Length < 1)
    {
        WriteUsage();
        return 1;
    }

    var operation = ReadOption(args, "--operation")
        ?? throw new ArgumentException("--operation is required for try.");

    var request = await new OpenApiRequestFactory().CreateRequestAsync(args[0], operation);
    var storage = new ReadableHttpJsonStorage();
    var context = await CreateContextAsync(storage, args);
    ApplyRequestOverrides(request, args);

    var outputRequestPath = ReadOption(args, "--output-request");
    if (!string.IsNullOrWhiteSpace(outputRequestPath))
    {
        await storage.SaveAsync(outputRequestPath, request);
    }

    var exchange = await new ReadableHttpExecutor().SendAsync(request, context);
    Console.WriteLine($"HTTP {(exchange.Response?.StatusCode.ToString() ?? "ERROR")} {exchange.Response?.ReasonPhrase}");

    if (exchange.Error is not null)
    {
        Console.Error.WriteLine($"{exchange.Error.Type}: {exchange.Error.Message}");
        return 2;
    }

    if (!string.IsNullOrEmpty(exchange.Response?.BodyText))
    {
        Console.WriteLine(exchange.Response.BodyText);
    }

    var outputPath = ReadOption(args, "--output");
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        await storage.SaveAsync(outputPath, exchange);
    }

    return exchange.Response?.StatusCode is >= 200 and < 400 ? 0 : 3;
}

static async Task<int> TryDocumentAsync(string[] args)
{
    if (args.Length < 1)
    {
        WriteUsage();
        return 1;
    }

    var document = await new ReadableTryDocumentLoader().LoadAsync(args[0]);
    Console.WriteLine($"{document.SourceType}: {document.Title ?? document.FileName}");
    foreach (var operation in document.Operations)
    {
        Console.WriteLine($"{operation.Method,-7} {operation.Path}  {operation.Name}");
    }

    var outputPath = ReadOption(args, "--output");
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        await new ReadableHttpJsonStorage().SaveAsync(outputPath, document);
    }

    return 0;
}

static async Task<int> InitAsync(string[] args)
{
    if (args.Length < 1)
    {
        WriteUsage();
        return 1;
    }

    var overwrite = HasOption(args, "--force");
    await new ReadableWorkspaceStore().InitializeExampleAsync(args[0], overwrite);
    Console.WriteLine($"Initialized example workspace at {args[0]}");
    return 0;
}

static async Task<int> ImportAsync(string[] args)
{
    if (args.Length < 2)
    {
        WriteUsage();
        return 1;
    }

    var format = args[0].ToLowerInvariant();
    var inputPath = args[1];
    var outputPath = ReadOption(args, "--output");
    var storage = new ReadableHttpJsonStorage();

    if (format == "http")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required for import.");
        }

        var requests = new HttpFileConverter().Import(await File.ReadAllTextAsync(inputPath));
        if (requests.Count == 1)
        {
            await storage.SaveAsync(outputPath, requests[0]);
        }
        else
        {
            Directory.CreateDirectory(outputPath);
            foreach (var request in requests)
            {
                await storage.SaveAsync(Path.Combine(outputPath, $"{ToFileName(request.Name)}.json"), request);
            }
        }

        return 0;
    }

    if (format == "curl")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required for import.");
        }

        var request = new CurlConverter().Import(await File.ReadAllTextAsync(inputPath));
        await storage.SaveAsync(outputPath, request);
        return 0;
    }

    if (format is "openapi" or "swagger")
    {
        var workspacePath = ReadOption(args, "--workspace");
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            var imported = await new ReadableWorkspaceStore(storage).ImportOpenApiAsync(
                workspacePath,
                inputPath,
                ReadOption(args, "--group"));
            Console.WriteLine($"Imported {imported.Count} requests into workspace.");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required for import.");
        }

        var requests = new OpenApiConverter().Import(
            await File.ReadAllTextAsync(inputPath),
            Path.GetExtension(inputPath));
        if (requests.Count == 1)
        {
            await storage.SaveAsync(outputPath, requests[0]);
        }
        else
        {
            Directory.CreateDirectory(outputPath);
            foreach (var request in requests)
            {
                await storage.SaveAsync(Path.Combine(outputPath, $"{ToFileName(request.Name)}.json"), request);
            }
        }

        return 0;
    }

    throw new ArgumentException($"Unknown import format '{format}'.");
}

static async Task<int> ExportAsync(string[] args)
{
    if (args.Length < 2)
    {
        WriteUsage();
        return 1;
    }

    var format = args[0].ToLowerInvariant();
    var requestPath = args[1];
    var outputPath = ReadOption(args, "--output");
    var storage = new ReadableHttpJsonStorage();
    var text = format switch
    {
        "http" => new HttpFileConverter().Export([await storage.LoadAsync<ReadableRequest>(requestPath)]),
        "curl" => new CurlConverter().Export(await storage.LoadAsync<ReadableRequest>(requestPath)),
        "openapi" or "swagger" => new OpenApiConverter().Export(await LoadRequestsForExportAsync(storage, requestPath)),
        _ => throw new ArgumentException($"Unknown export format '{format}'.")
    };

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine(text);
    }
    else
    {
        await File.WriteAllTextAsync(outputPath, text);
    }

    return 0;
}

static async Task<IReadOnlyList<ReadableRequest>> LoadRequestsForExportAsync(
    ReadableHttpJsonStorage storage,
    string path)
{
    if (File.Exists(path))
    {
        return [await storage.LoadAsync<ReadableRequest>(path)];
    }

    if (!Directory.Exists(path))
    {
        throw new FileNotFoundException($"Request path '{path}' was not found.");
    }

    var requests = new List<ReadableRequest>();
    foreach (var file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories))
    {
        requests.Add(await storage.LoadAsync<ReadableRequest>(file));
    }

    return requests;
}

static string ToFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? "request" : sanitized;
}

static async Task<ReadableRequest> LoadRequestAsync(ReadableHttpJsonStorage storage, string[] args)
{
    var workspacePath = ReadOption(args, "--workspace");
    if (string.IsNullOrWhiteSpace(workspacePath))
    {
        if (args.Length < 1)
        {
            throw new ArgumentException("Request path is required.");
        }

        return await storage.LoadAsync<ReadableRequest>(args[0]);
    }

    var requestName = ReadOption(args, "--request")
        ?? throw new ArgumentException("--request is required when --workspace is used.");

    return await new ReadableWorkspaceStore(storage).LoadRequestAsync(workspacePath, requestName);
}

static async Task<ReadableExecutionContext> CreateContextAsync(ReadableHttpJsonStorage storage, string[] args)
{
    var context = new ReadableExecutionContext();
    var envPath = ReadOption(args, "--env");
    if (!string.IsNullOrWhiteSpace(envPath))
    {
        var workspacePath = ReadOption(args, "--workspace");
        var environment = !string.IsNullOrWhiteSpace(workspacePath) && !File.Exists(envPath)
            ? await new ReadableWorkspaceStore(storage).LoadEnvironmentAsync(workspacePath, envPath)
                ?? throw new FileNotFoundException($"Environment '{envPath}' was not found in workspace '{workspacePath}'.")
            : await storage.LoadAsync<ReadableEnvironment>(envPath);

        context.Variables = environment.Variables;
    }

    foreach (var assignment in ReadOptions(args, "--var"))
    {
        var (name, value) = SplitAssignment(assignment, "--var");
        context.Variables[name] = value;
    }

    return context;
}

static void ApplyRequestOverrides(ReadableRequest request, string[] args)
{
    var method = ReadOption(args, "--method");
    if (!string.IsNullOrWhiteSpace(method))
    {
        request.Method = method;
    }

    var url = ReadOption(args, "--url");
    if (!string.IsNullOrWhiteSpace(url))
    {
        request.Url = url;
    }

    foreach (var assignment in ReadOptions(args, "--header"))
    {
        var (name, value) = SplitAssignment(assignment, "--header");
        request.Headers.Add(new ReadableNameValue { Name = name, Value = value, Enabled = true });
    }

    foreach (var assignment in ReadOptions(args, "--query"))
    {
        var (name, value) = SplitAssignment(assignment, "--query");
        request.Query.Add(new ReadableNameValue { Name = name, Value = value, Enabled = true });
    }

    var bodyPath = ReadOption(args, "--body-file");
    if (!string.IsNullOrWhiteSpace(bodyPath))
    {
        request.Body = new ReadableBody
        {
            Type = ReadableBodyType.Raw,
            Content = File.ReadAllText(bodyPath),
            ContentType = ReadOption(args, "--content-type") ?? "text/plain"
        };
    }
}

static ReadableStreamFormat ReadStreamFormat(string? value)
{
    return ReadableCliOptions.ReadStreamFormat(value);
}

static string? ReadOption(string[] args, string name)
{
    return ReadableCliOptions.ReadOption(args, name);
}

static IEnumerable<string> ReadOptions(string[] args, string name)
{
    return ReadableCliOptions.ReadOptions(args, name);
}

static bool HasOption(string[] args, string name)
{
    return ReadableCliOptions.HasOption(args, name);
}

static (string Name, string Value) SplitAssignment(string assignment, string optionName)
{
    return ReadableCliOptions.SplitAssignment(assignment, optionName);
}

static int WriteUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    WriteUsage();
    return 1;
}

static void WriteUsage()
{
    Console.WriteLine("ReadableHttp CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  readablehttp send <request.json> [options]");
    Console.WriteLine("  readablehttp stream <request.json> [options]");
    Console.WriteLine("  readablehttp try <openapi.json|yaml> --operation <operationId|METHOD path> [options]");
    Console.WriteLine("  readablehttp trydoc <openapi|http|curl|request-file> [--output <trydoc.json>]");
    Console.WriteLine("  readablehttp import <http|curl|openapi|swagger> <input> --output <request.json|directory> [--workspace <path>]");
    Console.WriteLine("  readablehttp export <http|curl|openapi|swagger> <request.json|directory> [--output <path>]");
    Console.WriteLine("  readablehttp init <workspace-path> [--force]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --env <environment.json>");
    Console.WriteLine("  --workspace <path>");
    Console.WriteLine("  --request <request-name-or-id>");
    Console.WriteLine("  --var <name=value>");
    Console.WriteLine("  --header <name=value>");
    Console.WriteLine("  --query <name=value>");
    Console.WriteLine("  --method <method>");
    Console.WriteLine("  --url <url>");
    Console.WriteLine("  --body-file <path>");
    Console.WriteLine("  --content-type <content-type>");
    Console.WriteLine("  --output <exchange.json>");
    Console.WriteLine("  --output-request <request.json>      try only");
    Console.WriteLine("  --format <auto|sse|lines|raw>       stream only");
}
