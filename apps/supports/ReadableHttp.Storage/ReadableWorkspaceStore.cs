using ReadableHttp;
using ReadableHttp.ImportExport;

namespace ReadableHttp.Storage;

public sealed class ReadableWorkspaceStore
{
    private readonly ReadableHttpJsonStorage _jsonStorage;

    public ReadableWorkspaceStore(ReadableHttpJsonStorage? jsonStorage = null)
    {
        _jsonStorage = jsonStorage ?? new ReadableHttpJsonStorage();
    }

    public Task<ReadableWorkspace> LoadWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        return _jsonStorage.LoadAsync<ReadableWorkspace>(
            Path.Combine(workspacePath, "workspace.json"),
            cancellationToken);
    }

    public Task SaveWorkspaceAsync(
        string workspacePath,
        ReadableWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        return _jsonStorage.SaveAsync(
            Path.Combine(workspacePath, "workspace.json"),
            workspace,
            cancellationToken);
    }

    public async Task<ReadableRequest> LoadRequestAsync(
        string workspacePath,
        string requestNameOrId,
        CancellationToken cancellationToken = default)
    {
        var requestFiles = Directory.EnumerateFiles(
            Path.Combine(workspacePath, "requests"),
            "*.json",
            SearchOption.AllDirectories);

        foreach (var file in requestFiles)
        {
            var request = await _jsonStorage.LoadAsync<ReadableRequest>(file, cancellationToken);
            if (Matches(request.Id, requestNameOrId) || Matches(request.Name, requestNameOrId))
            {
                return request;
            }
        }

        throw new FileNotFoundException($"Request '{requestNameOrId}' was not found in workspace '{workspacePath}'.");
    }

    public async Task<IReadOnlyList<ReadableRequest>> LoadCollectionRequestsAsync(
        string workspacePath,
        ReadableCollection collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var requestDirectory = GetCollectionRequestDirectory(workspacePath, collection);
        if (!Directory.Exists(requestDirectory))
        {
            return collection.Requests;
        }

        var requests = new List<ReadableRequest>();
        foreach (var file in Directory.EnumerateFiles(requestDirectory, "*.json", SearchOption.AllDirectories))
        {
            requests.Add(await _jsonStorage.LoadAsync<ReadableRequest>(file, cancellationToken));
        }

        collection.Requests = requests;
        return requests;
    }

    public async Task<IReadOnlyList<ReadableRequest>> LoadLooseRequestsAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var requestDirectory = Path.Combine(workspacePath, "requests");
        if (!Directory.Exists(requestDirectory))
        {
            return [];
        }

        var requests = new List<ReadableRequest>();
        foreach (var file in Directory.EnumerateFiles(requestDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            requests.Add(await _jsonStorage.LoadAsync<ReadableRequest>(file, cancellationToken));
        }

        return requests;
    }

    public async Task SaveCollectionRequestsAsync(
        string workspacePath,
        ReadableCollection collection,
        IEnumerable<ReadableRequest> requests,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var requestDirectory = GetCollectionRequestDirectory(workspacePath, collection);
        Directory.CreateDirectory(requestDirectory);

        if (replaceExisting)
        {
            foreach (var file in Directory.EnumerateFiles(requestDirectory, "*.json"))
            {
                File.Delete(file);
            }
        }

        collection.Requests = requests.ToList();
        foreach (var request in collection.Requests)
        {
            await _jsonStorage.SaveAsync(
                Path.Combine(requestDirectory, $"{ToFileName(request.Name)}.json"),
                request,
                cancellationToken);
        }
    }

    public Task DeleteCollectionRequestsAsync(
        string workspacePath,
        ReadableCollection collection,
        CancellationToken cancellationToken = default)
    {
        var requestDirectory = GetCollectionRequestDirectory(workspacePath, collection);
        if (Directory.Exists(requestDirectory))
        {
            Directory.Delete(requestDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    public async Task<ReadableEnvironment?> LoadEnvironmentAsync(
        string workspacePath,
        string environmentNameOrId,
        CancellationToken cancellationToken = default)
    {
        var environmentDirectory = Path.Combine(workspacePath, "environments");
        if (!Directory.Exists(environmentDirectory))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(environmentDirectory, "*.json", SearchOption.AllDirectories))
        {
            var environment = await _jsonStorage.LoadAsync<ReadableEnvironment>(file, cancellationToken);
            if (Matches(environment.Id, environmentNameOrId) || Matches(environment.Name, environmentNameOrId))
            {
                return environment;
            }
        }

        return null;
    }

    public async Task SaveExchangeAsync(
        string workspacePath,
        ReadableExchange exchange,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(workspacePath, "history"));
        var historyPath = Path.Combine(
            workspacePath,
            "history",
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{exchange.Request.Id}.json");

        await _jsonStorage.SaveAsync(historyPath, exchange, cancellationToken);
        await AppendHistoryEntryAsync(workspacePath, exchange, historyPath, cancellationToken);
    }

    public Task<ReadableSecretStore> LoadSecretsAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(workspacePath, ".readablehttp", "secrets.local.json");
        return File.Exists(path)
            ? _jsonStorage.LoadAsync<ReadableSecretStore>(path, cancellationToken)
            : Task.FromResult(new ReadableSecretStore());
    }

    public Task SaveSecretsAsync(string workspacePath, ReadableSecretStore secrets, CancellationToken cancellationToken = default)
    {
        return _jsonStorage.SaveAsync(
            Path.Combine(workspacePath, ".readablehttp", "secrets.local.json"),
            secrets,
            cancellationToken);
    }

    private async Task AppendHistoryEntryAsync(
        string workspacePath,
        ReadableExchange exchange,
        string exchangePath,
        CancellationToken cancellationToken)
    {
        var entry = new ReadableHistoryEntry
        {
            RequestId = exchange.Request.Id,
            RequestName = exchange.Request.Name,
            Method = exchange.Request.Method,
            Url = exchange.Request.Url,
            StatusCode = exchange.Response?.StatusCode,
            Duration = exchange.Response?.Duration ?? TimeSpan.Zero,
            StartedAt = exchange.StartedAt,
            ExchangePath = Path.GetRelativePath(workspacePath, exchangePath)
        };

        var indexPath = Path.Combine(workspacePath, "history", "index.jsonl");
        var json = System.Text.Json.JsonSerializer.Serialize(entry, ReadableHttpJsonStorage.JsonOptions);
        await File.AppendAllTextAsync(indexPath, json + Environment.NewLine, cancellationToken);
    }

    public async Task InitializeExampleAsync(
        string workspacePath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(workspacePath) && Directory.EnumerateFileSystemEntries(workspacePath).Any() && !overwrite)
        {
            throw new InvalidOperationException($"Workspace path '{workspacePath}' is not empty.");
        }

        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "collections"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "requests", "httpbin"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "environments"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "specs"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "imports"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "history"));
        Directory.CreateDirectory(Path.Combine(workspacePath, ".readablehttp"));

        await _jsonStorage.SaveAsync(Path.Combine(workspacePath, "workspace.json"), new ReadableWorkspace
        {
            Name = "ReadableHttp Example Workspace",
            Type = ReadableWorkspaceType.Local,
            Collections =
            [
                new ReadableCollection
                {
                    Name = "HTTPBin Examples",
                    SourceType = ReadableCollectionSourceType.Local,
                    RequestDirectory = "requests/httpbin"
                }
            ],
            Specifications =
            [
                new ReadableSpecification
                {
                    Name = "HTTPBin OpenAPI",
                    SourceType = ReadableSpecificationSourceType.LocalFile,
                    Format = ReadableSpecificationFormat.OpenApi,
                    Path = "specs/httpbin.openapi.json"
                },
                new ReadableSpecification
                {
                    Name = "Remote OpenAPI Example",
                    SourceType = ReadableSpecificationSourceType.RemoteEndpoint,
                    Format = ReadableSpecificationFormat.OpenApi,
                    Path = "specs/remote-openapi.json",
                    Remote = new ReadableRemoteSpecificationOptions
                    {
                        Endpoint = "https://httpbin.org/spec.json"
                    }
                }
            ]
        }, cancellationToken);

        await _jsonStorage.SaveAsync(Path.Combine(workspacePath, "environments", "dev.json"), new ReadableEnvironment
        {
            Name = "dev",
            Variables =
            {
                ["baseUrl"] = "https://httpbin.org",
                ["keyword"] = "readable-http",
                ["apiKey"] = "sample-api-key"
            }
        }, cancellationToken);

        await _jsonStorage.SaveAsync(Path.Combine(workspacePath, "requests", "httpbin", "get-with-query.json"), new ReadableRequest
        {
            Name = "get-with-query",
            Method = "GET",
            Url = "{{baseUrl}}/get",
            Query =
            [
                new ReadableNameValue { Name = "keyword", Value = "{{keyword}}" }
            ],
            Headers =
            [
                new ReadableNameValue { Name = "accept", Value = "application/json" }
            ]
        }, cancellationToken);

        await _jsonStorage.SaveAsync(Path.Combine(workspacePath, "requests", "httpbin", "post-json.json"), new ReadableRequest
        {
            Name = "post-json",
            Method = "POST",
            Url = "{{baseUrl}}/post",
            Headers =
            [
                new ReadableNameValue { Name = "accept", Value = "application/json" }
            ],
            Body = new ReadableBody
            {
                Type = ReadableBodyType.Json,
                ContentType = "application/json",
                Content = "{\n  \"name\": \"ReadableHttp\",\n  \"source\": \"example-workspace\"\n}"
            }
        }, cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(workspacePath, "specs", "httpbin.openapi.json"), CreateExampleOpenApi(), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(workspacePath, "imports", "httpbin.http"), CreateExampleHttpFile(), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(workspacePath, ".gitignore"), "history/\n.readablehttp/secrets.local.json\n", cancellationToken);
    }

    public async Task<IReadOnlyList<ReadableRequest>> ImportOpenApiAsync(
        string workspacePath,
        string specPath,
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(specPath);
        var content = await File.ReadAllTextAsync(specPath, cancellationToken);
        var requests = new OpenApiConverter().Import(content, extension);
        var group = ToFileName(groupName ?? Path.GetFileNameWithoutExtension(specPath));
        var requestDirectory = Path.Combine(workspacePath, "requests", group);
        var specDirectory = Path.Combine(workspacePath, "specs");

        Directory.CreateDirectory(requestDirectory);
        Directory.CreateDirectory(specDirectory);

        await File.WriteAllTextAsync(Path.Combine(specDirectory, Path.GetFileName(specPath)), content, cancellationToken);
        foreach (var request in requests)
        {
            await _jsonStorage.SaveAsync(
                Path.Combine(requestDirectory, $"{ToFileName(request.Name)}.json"),
                request,
                cancellationToken);
        }

        return requests;
    }

    private static string CreateExampleOpenApi()
    {
        return """
        {
          "openapi": "3.0.3",
          "info": {
            "title": "HTTPBin Example",
            "version": "1.0.0"
          },
          "servers": [
            {
              "url": "https://httpbin.org"
            }
          ],
          "paths": {
            "/get": {
              "get": {
                "operationId": "getWithQuery",
                "summary": "GET with query",
                "parameters": [
                  {
                    "name": "keyword",
                    "in": "query",
                    "schema": {
                      "type": "string"
                    }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "OK"
                  }
                }
              }
            }
          }
        }
        """;
    }

    private static string CreateExampleHttpFile()
    {
        return """
        ### Get with query
        GET {{baseUrl}}/get?keyword={{keyword}}
        accept: application/json

        ### Post JSON
        POST {{baseUrl}}/post
        accept: application/json
        content-type: application/json

        {
          "name": "ReadableHttp",
          "source": "example-workspace"
        }
        """;
    }

    private static string ToFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }

    private static string GetCollectionRequestDirectory(string workspacePath, ReadableCollection collection)
    {
        return string.IsNullOrWhiteSpace(collection.RequestDirectory)
            ? Path.Combine(workspacePath, "requests", ToFileName(collection.Name))
            : Path.Combine(workspacePath, collection.RequestDirectory);
    }

    private static bool Matches(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
