# ReadableHttp

ReadableHttp is a .NET-first HTTP toolkit for readable SDK calls, raw request execution, and future API-client workflows.

The first migration splits the old `FluentHttp.Json` package into a small set of focused projects:

- `apps/ReadableHttp.App.Maui`: MAUI Blazor Hybrid desktop app shell.
- `ReadableHttp.Core`: JSON-serializable request, response, exchange, auth, environment, collection, and workspace models.
- `ReadableHttp.Execution`: raw execution engine that preserves complete responses, including non-2xx responses.
- `ReadableHttp`: fluent SDK surface for direct code usage.
- `ReadableHttp.Storage`: JSON load/save helpers for request and workspace files.
- `ReadableHttp.AspNetCore`: `IHttpClientFactory` and DI integration.
- `ReadableHttp.Cli`: command-line request execution.

## SDK

```csharp
using ReadableHttp;

var exchange = await ReadableHttpClient
    .Request("https://api.example.com/users")
    .Get()
    .WithBearerToken(token)
    .SendExchangeAsync();
```

For business-code style calls, use `SendAsync<T>()`:

```csharp
using ReadableHttp;

var user = await ReadableHttpClient
    .Request("https://api.example.com/users/1")
    .Get()
    .SendAsync<UserDto>();
```

Streaming APIs such as Server-Sent Events can be consumed without buffering the full response:

```csharp
using ReadableHttp;
using ReadableHttp.Core;

await foreach (var message in ReadableHttpClient
    .Request("https://api.example.com/chat")
    .Post()
    .WithJsonBody(new { query = "hello", response_mode = "streaming" })
    .StreamAsync())
{
    if (message.Type == ReadableStreamMessageType.Data)
    {
        Console.WriteLine(message.Data);
    }
}
```

## Request JSON

```json
{
  "schemaVersion": "1.0",
  "name": "Get user",
  "method": "GET",
  "url": "{{baseUrl}}/users/{{userId}}",
  "headers": [
    { "name": "accept", "value": "application/json", "enabled": true }
  ],
  "auth": {
    "type": "bearer",
    "token": "{{token}}"
  }
}
```

## Environment JSON

```json
{
  "schemaVersion": "1.0",
  "name": "dev",
  "variables": {
    "baseUrl": {
      "value": "https://api.example.com",
      "type": "string"
    },
    "userId": {
      "value": "1",
      "type": "string"
    },
    "token": {
      "value": "local-token",
      "type": "string"
    }
  }
}
```

Stable JSON schemas for request, environment, and workspace files are published under `schemas/` and are included in the NuGet packages. Newly saved files use the current format version.

## CLI

```shell
dotnet run --project src/ReadableHttp.Cli -- send ./requests/get-user.json --env ./environments/dev.json
```

Useful CLI options:

```shell
dotnet run --project src/ReadableHttp.Cli -- send samples/requests/basic/get-with-query.json --env samples/environments/httpbin.json --header x-demo=true --output exchange.json
dotnet run --project src/ReadableHttp.Cli -- stream samples/requests/streaming/get-lines.json --env samples/environments/httpbin.json --format lines
dotnet run --project src/ReadableHttp.Cli -- try samples/openapi/httpbin.openapi.json --operation getAnything --var keyword=openapi
dotnet run --project src/ReadableHttp.Cli -- trydoc samples/openapi/httpbin.openapi.json
dotnet run --project src/ReadableHttp.Cli -- trydoc samples/http/httpbin.http
dotnet run --project src/ReadableHttp.Cli -- send --workspace samples/workspace --request get-with-query --env dev
dotnet run --project src/ReadableHttp.Cli -- init ./my-workspace
dotnet run --project src/ReadableHttp.Cli -- import http samples/http/httpbin.http --output samples/imported-http
dotnet run --project src/ReadableHttp.Cli -- export curl samples/requests/bodies/post-json.json
```

`trydoc` normalizes local files in memory for future UI usage. Supported inputs include OpenAPI/Swagger JSON/YAML, `.http`, curl command files, and ReadableHttp request JSON. A UI can show the raw file and a normalized Try view from the same `ReadableTryDocument`.

Workspaces can be local or Git-backed. Git workspaces expose status, pull, and push operations through `ReadableWorkspaceGitService`.

Workspace content is split by responsibility:

- Collections are editable request sets. They load and save request JSON files under `requests/<collection-name>/` or the collection's `requestDirectory`.
- Specifications are API contract sources such as OpenAPI, Swagger, `.http`, curl, or ReadableHttp request files. They can point at a local file or a remote endpoint, refresh into `specs/`, and normalize into a Try document without turning the source into an editable collection.
- Specification sources track SHA-256 checksums. Remote refreshes save the source file and normalized Try document using checksum-based file names, then update the workspace paths to the latest version.
- Operations from a specification can be tried directly or saved into a collection when you want an editable copy.

Sample files and runnable projects are available under `samples/`:

- `samples/ReadableHttp.ConsoleSample`: direct SDK usage.
- `samples/ReadableHttp.WebApiSample`: ASP.NET Core DI and `IReadableHttpFactory` usage.
- `samples/requests`: request JSON files for GET, POST, PUT, PATCH, DELETE, auth, body types, and streaming.
