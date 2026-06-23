namespace ReadableHttp;

public enum ReadableWorkspaceType
{
    Local,
    Git
}

public enum ReadableCollectionSourceType
{
    Local
}

public enum ReadableSpecificationSourceType
{
    LocalFile,
    RemoteEndpoint,
    GitFile
}

public enum ReadableSpecificationFormat
{
    Unknown,
    OpenApi,
    Swagger,
    Http,
    Curl,
    ReadableRequest,
    Har,
    PostmanCollection
}

public sealed class ReadableWorkspace
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "ReadableHttp Workspace";

    public ReadableWorkspaceType Type { get; set; } = ReadableWorkspaceType.Local;

    public ReadableGitOptions? Git { get; set; }

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];

    public List<ReadableCollection> Collections { get; set; } = [];

    public List<ReadableSpecification> Specifications { get; set; } = [];

    public List<ReadableEnvironment> Environments { get; set; } = [];
}

public sealed class ReadableCollection
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Collection";

    public ReadableCollectionSourceType SourceType { get; set; } = ReadableCollectionSourceType.Local;

    public string? RequestDirectory { get; set; }

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];

    public List<ReadableRequest> Requests { get; set; } = [];
}

public sealed class ReadableSpecification
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Specification";

    public ReadableSpecificationSourceType SourceType { get; set; } = ReadableSpecificationSourceType.LocalFile;

    public ReadableSpecificationFormat Format { get; set; } = ReadableSpecificationFormat.Unknown;

    public string? Path { get; set; }

    public string? NormalizedPath { get; set; }

    public string? Checksum { get; set; }

    public ReadableRemoteSpecificationOptions? Remote { get; set; }

    public ReadableGitSpecificationOptions? Git { get; set; }

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];

    public DateTimeOffset? LastNormalizedAt { get; set; }
}

public sealed class ReadableGitOptions
{
    public string? RemoteUrl { get; set; }

    public string Branch { get; set; } = "main";

    public bool AutoPullOnOpen { get; set; }

    public bool AutoPushOnSave { get; set; }
}

public sealed class ReadableRemoteSpecificationOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public List<ReadableNameValue> Headers { get; set; } = [];

    public DateTimeOffset? LastRefreshedAt { get; set; }

    public string? ETag { get; set; }

    public string? Checksum { get; set; }

    public bool UpdateAvailable { get; set; }
}

public sealed class ReadableGitSpecificationOptions
{
    public string? RemoteUrl { get; set; }

    public string Branch { get; set; } = "main";

    public string Path { get; set; } = string.Empty;
}

public sealed class ReadableEnvironment
{
    public string SchemaVersion { get; set; } = ReadableHttpFormat.CurrentSchemaVersion;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Environment";

    public Dictionary<string, ReadableVariable> Variables { get; set; } = [];
}
