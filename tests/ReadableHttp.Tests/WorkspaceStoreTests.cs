using ReadableHttp;
using ReadableHttp.Storage;

namespace ReadableHttp.Tests;

public sealed class WorkspaceStoreTests
{
    [Fact]
    public async Task InitializeExampleAsync_creates_workspace_structure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();

        await store.InitializeExampleAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(path, "workspace.json")));
        Assert.True(File.Exists(Path.Combine(path, "environments", "dev.json")));
        Assert.True(File.Exists(Path.Combine(path, "collections", "httpbin", "get-with-query.json")));
        Assert.True(File.Exists(Path.Combine(path, "specs", "httpbin.openapi.json")));
        Assert.True(File.Exists(Path.Combine(path, "specs", "httpbin.http")));

        var workspace = await store.LoadWorkspaceAsync(path, TestContext.Current.CancellationToken);
        Assert.Single(workspace.Collections);
        Assert.Equal("collections/httpbin", workspace.Collections[0].RequestDirectory);
        Assert.Equal(2, workspace.Specifications.Count);
        Assert.Equal(ReadableSpecificationSourceType.LocalFile, workspace.Specifications[0].SourceType);
    }

    [Fact]
    public async Task SaveExchangeAsync_writes_exchange_and_history_index()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        await store.InitializeExampleAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        await store.SaveExchangeAsync(path, new ReadableExchange
        {
            Request = new ReadableRequest
            {
                Name = "Get",
                Method = "GET",
                Url = "https://api.example.test"
            },
            Response = new ReadableResponse
            {
                StatusCode = 200,
                Duration = TimeSpan.FromMilliseconds(12)
            },
            StartedAt = DateTimeOffset.UtcNow
        }, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(path, "history", "index.jsonl")));
    }

    [Fact]
    public async Task SaveSecretsAsync_roundtrips_local_secrets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        await store.InitializeExampleAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        await store.SaveSecretsAsync(path, new ReadableSecretStore
        {
            WorkspaceId = "workspace",
            Values =
            {
                ["token"] = "secret"
            }
        }, TestContext.Current.CancellationToken);

        var secrets = await store.LoadSecretsAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("secret", secrets.Values["token"]);
    }

    [Fact]
    public async Task SaveCollectionRequestsAsync_writes_local_collection_requests()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        var collection = new ReadableCollection
        {
            Name = "Local API",
            SourceType = ReadableCollectionSourceType.Local
        };

        await store.SaveCollectionRequestsAsync(
            path,
            collection,
            [
                new ReadableRequest
                {
                    Name = "Get User",
                    Method = "GET",
                    Url = "https://api.example.test/users/1"
                }
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        var requests = await store.LoadCollectionRequestsAsync(path, collection, TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("Get User", request.Name);
        Assert.True(File.Exists(Path.Combine(path, "collections", "Local API", "Get User.json")));
    }

    [Fact]
    public async Task SaveRequestAsync_writes_one_request_without_deleting_neighbors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        var collection = new ReadableCollection
        {
            Name = "Local API",
            SourceType = ReadableCollectionSourceType.Local
        };

        var first = new ReadableRequest
        {
            Name = "First",
            Method = "GET",
            Url = "https://api.example.test/first"
        };
        var second = new ReadableRequest
        {
            Name = "Second",
            Method = "GET",
            Url = "https://api.example.test/second"
        };

        await store.SaveRequestAsync(path, collection, first, TestContext.Current.CancellationToken);
        await store.SaveRequestAsync(path, collection, second, TestContext.Current.CancellationToken);

        first.Url = "https://api.example.test/first-updated";
        await store.SaveRequestAsync(path, collection, first, TestContext.Current.CancellationToken);

        var requests = await store.LoadCollectionRequestsAsync(path, collection, TestContext.Current.CancellationToken);

        Assert.Equal(2, requests.Count);
        Assert.Contains(requests, request => request.Name == "First" && request.Url.EndsWith("/first-updated", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Name == "Second" && request.Url.EndsWith("/second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveRequestAsync_renames_file_when_request_name_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        var collection = new ReadableCollection
        {
            Name = "Local API",
            SourceType = ReadableCollectionSourceType.Local
        };
        var request = new ReadableRequest
        {
            Name = "Old Name",
            Method = "GET",
            Url = "https://api.example.test/old"
        };

        await store.SaveRequestAsync(path, collection, request, TestContext.Current.CancellationToken);
        request.Name = "New Name";
        await store.SaveRequestAsync(path, collection, request, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(path, "collections", "Local API", "Old Name.json")));
        Assert.True(File.Exists(Path.Combine(path, "collections", "Local API", "New Name.json")));
        Assert.Equal("collections/Local API/New Name.json", request.SourcePath);
    }

    [Fact]
    public async Task SaveWorkspaceAsync_does_not_persist_request_cache()
    {
        var path = Path.Combine(Path.GetTempPath(), $"readable-http-workspace-{Guid.NewGuid():N}");
        var store = new ReadableWorkspaceStore();
        var workspace = new ReadableWorkspace
        {
            Name = "Workspace",
            Collections =
            [
                new ReadableCollection
                {
                    Name = "Loose",
                    RequestDirectory = "collections",
                    Requests =
                    [
                        new ReadableRequest
                        {
                            Name = "Cached",
                            Method = "GET",
                            Url = "https://api.example.test"
                        }
                    ]
                }
            ]
        };

        await store.SaveWorkspaceAsync(path, workspace, TestContext.Current.CancellationToken);

        var loaded = await store.LoadWorkspaceAsync(path, TestContext.Current.CancellationToken);
        Assert.Empty(loaded.Collections[0].Requests);
    }
}
