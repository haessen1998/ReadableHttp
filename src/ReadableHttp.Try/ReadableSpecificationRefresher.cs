using ReadableHttp.Core;

namespace ReadableHttp.Try;

public sealed class ReadableSpecificationRefresher
{
    private readonly HttpClient _httpClient;

    public ReadableSpecificationRefresher(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ReadableTryDocument> RefreshAsync(
        string workspacePath,
        ReadableSpecification specification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);

        var content = specification.SourceType switch
        {
            ReadableSpecificationSourceType.LocalFile => await ReadLocalAsync(workspacePath, specification, cancellationToken),
            ReadableSpecificationSourceType.RemoteEndpoint => await ReadRemoteAsync(workspacePath, specification, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported specification source '{specification.SourceType}'.")
        };

        var extension = GuessExtension(specification);
        var document = new ReadableTryDocumentLoader().Load(content, specification.Name, extension);
        foreach (var variable in specification.Variables)
        {
            document.Variables[variable.Key] = variable.Value;
        }

        specification.LastNormalizedAt = DateTimeOffset.UtcNow;
        return document;
    }

    private static async Task<string> ReadLocalAsync(
        string workspacePath,
        ReadableSpecification specification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(specification.Path))
        {
            throw new InvalidOperationException("Local specifications require Path.");
        }

        return await File.ReadAllTextAsync(
            ResolveWorkspacePath(workspacePath, specification.Path),
            cancellationToken);
    }

    private async Task<string> ReadRemoteAsync(
        string workspacePath,
        ReadableSpecification specification,
        CancellationToken cancellationToken)
    {
        if (specification.Remote is null || string.IsNullOrWhiteSpace(specification.Remote.Endpoint))
        {
            throw new InvalidOperationException("Remote specifications require an endpoint.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(specification.Remote.Method), specification.Remote.Endpoint);
        foreach (var header in specification.Remote.Headers.Where(header => header.Enabled))
        {
            request.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        specification.Remote.LastRefreshedAt = DateTimeOffset.UtcNow;
        specification.Remote.ETag = response.Headers.ETag?.Tag;

        if (!string.IsNullOrWhiteSpace(specification.Path))
        {
            var path = ResolveWorkspacePath(workspacePath, specification.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? workspacePath);
            await File.WriteAllTextAsync(path, content, cancellationToken);
        }

        return content;
    }

    private static string ResolveWorkspacePath(string workspacePath, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(workspacePath, path);
    }

    private static string GuessExtension(ReadableSpecification specification)
    {
        if (!string.IsNullOrWhiteSpace(specification.Path))
        {
            var extension = Path.GetExtension(specification.Path);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }
        }

        return specification.Format switch
        {
            ReadableSpecificationFormat.Http => ".http",
            ReadableSpecificationFormat.Curl => ".curl",
            ReadableSpecificationFormat.ReadableRequest => ".json",
            ReadableSpecificationFormat.OpenApi or ReadableSpecificationFormat.Swagger => ".json",
            _ => ".json"
        };
    }
}
