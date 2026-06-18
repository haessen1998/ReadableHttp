using ReadableHttp.Try;
using ReadableHttp.Core;

namespace ReadableHttp.Tests;

public sealed class TryDocumentTests
{
    [Fact]
    public void Load_normalizes_http_file()
    {
        var document = new ReadableTryDocumentLoader().Load("""
        ### Get Users
        GET https://api.example.test/users
        accept: application/json
        """, "api.http", ".http");

        Assert.Equal(ReadableTrySourceType.HttpFile, document.SourceType);
        var operation = Assert.Single(document.Operations);
        Assert.Equal("GET", operation.Method);
        Assert.Equal("https://api.example.test/users", operation.Path);
    }

    [Fact]
    public void Load_normalizes_curl()
    {
        var document = new ReadableTryDocumentLoader().Load(
            "curl -X POST 'https://api.example.test/users' --data-raw '{\"name\":\"demo\"}'",
            "post.curl",
            ".curl");

        Assert.Equal(ReadableTrySourceType.Curl, document.SourceType);
        var operation = Assert.Single(document.Operations);
        Assert.Equal("POST", operation.Method);
    }

    [Fact]
    public async Task SpecificationRefresher_loads_local_specification_as_try_document()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var specPath = Path.Combine(workspacePath, "specs", "demo.openapi.json");
        Directory.CreateDirectory(Path.GetDirectoryName(specPath)!);
        await File.WriteAllTextAsync(specPath, """
        {
          "openapi": "3.0.3",
          "info": { "title": "Demo", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.test" } ],
          "paths": {
            "/users": {
              "get": {
                "operationId": "getUsers",
                "summary": "Get users",
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """, TestContext.Current.CancellationToken);

        var specification = new ReadableSpecification
        {
            Name = "Demo",
            SourceType = ReadableSpecificationSourceType.LocalFile,
            Format = ReadableSpecificationFormat.OpenApi,
            Path = "specs/demo.openapi.json",
            Variables =
            {
                ["baseUrl"] = "https://api.example.test"
            }
        };

        var document = await new ReadableSpecificationRefresher().RefreshAsync(
            workspacePath,
            specification,
            TestContext.Current.CancellationToken);

        Assert.Equal(ReadableTrySourceType.OpenApi, document.SourceType);
        Assert.Equal("https://api.example.test", document.Variables["baseUrl"].ToTemplateValue());
        Assert.NotNull(specification.LastNormalizedAt);
        Assert.NotNull(specification.Checksum);
        Assert.NotNull(specification.NormalizedPath);
        Assert.True(File.Exists(Path.Combine(workspacePath, specification.NormalizedPath)));
        var operation = Assert.Single(document.Operations);
        Assert.Equal("GET", operation.Method);
    }

    [Fact]
    public async Task SpecificationRefresher_saves_remote_source_and_normalized_document_by_checksum()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "openapi": "3.0.3",
              "info": { "title": "Remote Demo", "version": "1.0.0" },
              "servers": [ { "url": "https://api.example.test" } ],
              "paths": {
                "/ping": {
                  "get": {
                    "operationId": "ping",
                    "responses": { "200": { "description": "OK" } }
                  }
                }
              }
            }
            """)
        }));
        var specification = new ReadableSpecification
        {
            Name = "Remote Demo",
            SourceType = ReadableSpecificationSourceType.RemoteEndpoint,
            Format = ReadableSpecificationFormat.OpenApi,
            Remote = new ReadableRemoteSpecificationOptions
            {
                Endpoint = "https://api.example.test/openapi.json"
            }
        };

        var document = await new ReadableSpecificationRefresher(new HttpClient(handler)).RefreshAsync(
            workspacePath,
            specification,
            TestContext.Current.CancellationToken);

        Assert.Equal(ReadableTrySourceType.OpenApi, document.SourceType);
        Assert.NotNull(specification.Checksum);
        Assert.NotNull(specification.Path);
        Assert.NotNull(specification.NormalizedPath);
        Assert.Equal(specification.Checksum, specification.Remote?.Checksum);
        Assert.False(specification.Remote?.UpdateAvailable);
        Assert.Contains(specification.Checksum![..12], specification.Path);
        Assert.Contains(specification.Checksum[..12], specification.NormalizedPath);
        Assert.True(File.Exists(Path.Combine(workspacePath, specification.Path)));
        Assert.True(File.Exists(Path.Combine(workspacePath, specification.NormalizedPath)));
    }
}
