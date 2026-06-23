namespace ReadableHttp.Core;

public enum ReadableBodyType
{
    None,
    Raw,
    Json,
    Xml,
    Html,
    Javascript,
    FormUrlEncoded,
    MultipartFormData,
    BinaryFile,
    Graphql
}

public sealed class ReadableBody
{
    public ReadableBodyType Type { get; set; } = ReadableBodyType.None;

    public string? Content { get; set; }

    public string? ContentType { get; set; }

    public string? FilePath { get; set; }

    public List<ReadableNameValue> Form { get; set; } = [];

    public List<ReadableMultipartItem> Multipart { get; set; } = [];

    public ReadableGraphqlBody? Graphql { get; set; }
}

public sealed class ReadableGraphqlBody
{
    public string Query { get; set; } = string.Empty;

    public string? Variables { get; set; }

    public string? OperationName { get; set; }
}

public enum ReadableMultipartItemType
{
    Text,
    File
}

public sealed class ReadableMultipartItem
{
    public string Name { get; set; } = string.Empty;

    public ReadableMultipartItemType Type { get; set; } = ReadableMultipartItemType.Text;

    public string? Value { get; set; }

    public string? FilePath { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public bool Enabled { get; set; } = true;
}
