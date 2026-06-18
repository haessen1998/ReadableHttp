using ReadableHttp.Core;
using ReadableHttp.Storage;

namespace ReadableHttp.Tests;

public sealed class ReadableHttpJsonStorageTests
{
    [Fact]
    public async Task SaveAsync_and_LoadAsync_roundtrip_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-{Guid.NewGuid():N}", "request.json");
        var storage = new ReadableHttpJsonStorage();
        var request = new ReadableRequest
        {
            Name = "Roundtrip",
            Method = "POST",
            Url = "{{baseUrl}}/post",
            Body = new ReadableBody
            {
                Type = ReadableBodyType.Json,
                Content = "{\"ok\":true}"
            }
        };

        await storage.SaveAsync(path, request, TestContext.Current.CancellationToken);
        var loaded = await storage.LoadAsync<ReadableRequest>(path, TestContext.Current.CancellationToken);

        Assert.Equal("Roundtrip", loaded.Name);
        Assert.Equal("POST", loaded.Method);
        Assert.Equal(ReadableHttpFormat.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(ReadableBodyType.Json, loaded.Body?.Type);
    }

    [Fact]
    public async Task LoadAsync_reads_object_variables()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"readable-http-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "environment.json");
        await File.WriteAllTextAsync(path, """
        {
          "name": "dev",
          "variables": {
            "baseUrl": {
              "value": "https://api.example.test",
              "type": "string"
            },
            "enabledFlag": {
              "value": true,
              "type": "boolean",
              "description": "Feature switch"
            },
            "payload": {
              "value": { "id": 42 },
              "type": "json"
            }
          }
        }
        """, TestContext.Current.CancellationToken);

        var environment = await new ReadableHttpJsonStorage().LoadAsync<ReadableEnvironment>(
            path,
            TestContext.Current.CancellationToken);

        Assert.Equal("https://api.example.test", environment.Variables["baseUrl"].ToTemplateValue());
        Assert.Equal("true", environment.Variables["enabledFlag"].ToTemplateValue()?.ToLowerInvariant());
        Assert.Equal("{\"id\":42}", environment.Variables["payload"].ToTemplateValue());
    }

    [Fact]
    public async Task LoadAsync_rejects_legacy_simple_variable_values()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"readable-http-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "environment.json");
        await File.WriteAllTextAsync(path, """
        {
          "name": "dev",
          "variables": {
            "baseUrl": "https://api.example.test"
          }
        }
        """, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new ReadableHttpJsonStorage().LoadAsync<ReadableEnvironment>(
            path,
            TestContext.Current.CancellationToken));
    }
}
