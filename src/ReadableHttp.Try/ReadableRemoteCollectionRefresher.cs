using ReadableHttp.Core;
using ReadableHttp.Storage;

namespace ReadableHttp.Try;

public sealed class ReadableRemoteCollectionRefresher
{
    private readonly HttpClient _httpClient;
    private readonly ReadableHttpJsonStorage _storage;

    public ReadableRemoteCollectionRefresher(HttpClient? httpClient = null, ReadableHttpJsonStorage? storage = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _storage = storage ?? new ReadableHttpJsonStorage();
    }

    public async Task<IReadOnlyList<ReadableRequest>> RefreshAsync(
        string workspacePath,
        ReadableCollection collection,
        CancellationToken cancellationToken = default)
    {
        // Remote collection refresh is kept for backward compatibility. New workspaces should
        // use ReadableSpecificationRefresher and then save selected operations into collections.
        if (collection.SourceType != ReadableCollectionSourceType.RemoteEndpoint)
        {
            throw new InvalidOperationException("Only remote endpoint collections can be refreshed.");
        }

        if (collection.Remote is null || string.IsNullOrWhiteSpace(collection.Remote.Endpoint))
        {
            throw new InvalidOperationException("Remote collection requires an endpoint.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(collection.Remote.Method), collection.Remote.Endpoint);
        foreach (var header in collection.Remote.Headers.Where(header => header.Enabled))
        {
            request.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var extension = GuessExtension(collection.Remote.Endpoint, response.Content.Headers.ContentType?.MediaType);
        var document = new ReadableTryDocumentLoader().Load(content, collection.Name, extension);
        var requests = document.Operations.Select(operation => operation.Request).ToList();

        collection.Requests = requests;
        collection.Remote.LastRefreshedAt = DateTimeOffset.UtcNow;

        var directory = Path.Combine(workspacePath, "requests", ToFileName(collection.Name));
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            File.Delete(file);
        }

        foreach (var readableRequest in requests)
        {
            await _storage.SaveAsync(
                Path.Combine(directory, $"{ToFileName(readableRequest.Name)}.json"),
                readableRequest,
                cancellationToken);
        }

        return requests;
    }

    private static string GuessExtension(string endpoint, string? mediaType)
    {
        var path = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.AbsolutePath : endpoint;
        var extension = Path.GetExtension(path);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        if (mediaType?.Contains("yaml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ".yaml";
        }

        return ".json";
    }

    private static string ToFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "collection" : sanitized;
    }
}
