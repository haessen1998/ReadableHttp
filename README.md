# ReadableHttp

ReadableHttp is a .NET-first HTTP toolkit for readable SDK calls, raw request execution, workspace files, and future API-client workflows.

The repository layout separates distributable SDK packages, optional extensions, application surfaces, application support libraries, and publish outputs:

- `src/ReadableHttp`: the main NuGet package. It contains the public SDK surface, request/response models, execution engine, variable resolution, streaming, and schema assets.
- `src/ReadableHttp.AspNetCore`: ASP.NET Core `IHttpClientFactory` and DI integration for the main SDK.
- `extensions/ReadableHttp.Auth`: OAuth2, PKCE, token cache, and loopback callback helpers.
- `extensions/ReadableHttp.ImportExport`: `.http`, curl, OpenAPI/Swagger, and request import/export helpers, organized by import/export format.
- `apps/supports/ReadableHttp.Storage`: JSON load/save helpers for request and workspace files.
- `apps/supports/ReadableHttp.Try`: in-memory Try document normalization for OpenAPI, `.http`, curl, and ReadableHttp request files.
- `apps/supports/ReadableHttp.AI`: extension contracts for AI-assisted request generation and response analysis.
- `apps/cli/ReadableHttp.Cli`: command-line request execution.
- `samples/sdks/ReadableHttp.ConsoleSample`: direct SDK usage sample app.
- `samples/sdks/ReadableHttp.WebApiSample`: ASP.NET Core DI sample app.
- `apps/maui/ReadableHttp.App.Maui`: MAUI Blazor Hybrid desktop app shell.
- `samples/workspaces/example`: the single example workspace, including requests, environments, imports, and specifications.
- `publish/maui`, `publish/cli`, `publish/nugets`: ignored output folders for publish artifacts.

Inside the main package, source folders keep the boundaries clear without preserving the old package layout:

- `src/ReadableHttp/Client`: fluent SDK facade for direct code usage.
- `src/ReadableHttp/Models`: JSON-serializable request, response, exchange, auth, variable, stream, and workspace models grouped by domain.
- `src/ReadableHttp/Execution`: raw execution engine, request materialization, variable resolution, redirects, cookies, response capture, and streaming.
- `src/ReadableHttp/Formatting`: shared request/response formatting helpers.

Inside `ReadableHttp.ImportExport`, format-specific code is grouped under `Curl`, `HttpFiles`, and `OpenApi`.

Model types live in the main `ReadableHttp` namespace. Execution contracts and executors live under `ReadableHttp.Execution`.

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

Variables support fixed values, host-evaluated expressions, and AI-assisted generation metadata:

```json
{
  "orderId": {
    "value": null,
    "type": "string",
    "source": "ai",
    "ai": {
      "purpose": "testParameter",
      "businessMeaning": "Unique order identifier",
      "avoidPreviouslyUsedValues": true
    }
  }
}
```

AI execution lives outside the core file format. `apps/supports/ReadableHttp.AI` defines extension contracts for generating business-meaningful test parameters, explaining responses, suggesting request adjustments after errors, and analyzing request/response history.

## ASP.NET Core

Use `ReadableHttp.AspNetCore` when you want `IHttpClientFactory` integration and DI-friendly executors:

```csharp
using ReadableHttp.AspNetCore;
using ReadableHttp;
using ReadableHttp.Execution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReadableHttp(client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.MapGet("/users/{id}", async (
    string id,
    IReadableHttpExecutor executor,
    CancellationToken cancellationToken) =>
{
    var exchange = await executor.SendAsync(
        new ReadableRequest
        {
            Method = "GET",
            Url = $"/users/{id}"
        },
        cancellationToken: cancellationToken);

    return Results.Json(new
    {
        status = exchange.Response?.StatusCode,
        body = exchange.Response?.BodyText,
        error = exchange.Error?.Message
    });
});
```

For multiple upstream APIs, register named `HttpClient` instances and create named executors:

```csharp
builder.Services.AddReadableHttp();
builder.Services.AddHttpClient("billing", client =>
{
    client.BaseAddress = new Uri("https://billing.example.com");
});

app.MapGet("/billing/ping", async (
    IReadableHttpFactory factory,
    CancellationToken cancellationToken) =>
{
    var executor = factory.CreateExecutor("billing");
    return await executor.SendAsync(
        new ReadableRequest { Method = "GET", Url = "/health" },
        cancellationToken: cancellationToken);
});
```

## CLI

```shell
dotnet run --project apps/cli/ReadableHttp.Cli -- send samples/workspaces/example/requests/httpbin/get-with-query.json --env samples/workspaces/example/environments/dev.json
```

Useful CLI options:

```shell
dotnet run --project apps/cli/ReadableHttp.Cli -- send samples/workspaces/example/requests/basic/get-with-query.json --env samples/workspaces/example/environments/httpbin.json --header x-demo=true --output exchange.json
dotnet run --project apps/cli/ReadableHttp.Cli -- stream samples/workspaces/example/requests/streaming/get-lines.json --env samples/workspaces/example/environments/httpbin.json --format lines
dotnet run --project apps/cli/ReadableHttp.Cli -- try samples/workspaces/example/specs/httpbin.openapi.json --operation getAnything --var keyword=openapi
dotnet run --project apps/cli/ReadableHttp.Cli -- trydoc samples/workspaces/example/specs/httpbin-full.openapi.json
dotnet run --project apps/cli/ReadableHttp.Cli -- trydoc samples/workspaces/example/imports/httpbin-standalone.http
dotnet run --project apps/cli/ReadableHttp.Cli -- send --workspace samples/workspaces/example --request get-with-query --env dev
dotnet run --project apps/cli/ReadableHttp.Cli -- init ./my-workspace
dotnet run --project apps/cli/ReadableHttp.Cli -- import http samples/workspaces/example/imports/httpbin-standalone.http --output samples/workspaces/example/imports/imported-http
dotnet run --project apps/cli/ReadableHttp.Cli -- export curl samples/workspaces/example/requests/bodies/post-json.json
```

`trydoc` normalizes local files in memory for future UI usage. Supported inputs include OpenAPI/Swagger JSON/YAML, `.http`, curl command files, and ReadableHttp request JSON. A UI can show the raw file and a normalized Try view from the same `ReadableTryDocument`.

Workspaces can be local or Git-backed. Git workspaces expose status, pull, and push operations through `ReadableWorkspaceGitService`.

Workspace content is split by responsibility:

- Collections are editable request sets. They load and save request JSON files under `requests/<collection-name>/` or the collection's `requestDirectory`.
- Specifications are API contract sources such as OpenAPI, Swagger, `.http`, curl, or ReadableHttp request files. They can point at a local file or a remote endpoint, refresh into `specs/`, and normalize into a Try document without turning the source into an editable collection.
- Specification sources track SHA-256 checksums. Remote refreshes save the source file and normalized Try document using checksum-based file names, then update the workspace paths to the latest version.
- Operations from a specification can be tried directly or saved into a collection when you want an editable copy.

Sample content under `samples/` is split into SDK sample projects under `samples/sdks` and the consolidated `samples/workspaces/example` workspace.
