using ReadableHttp.Execution;

namespace ReadableHttp.AspNetCore;

public interface IReadableHttpFactory
{
    HttpClient CreateClient(string name);

    IReadableHttpExecutor CreateExecutor(string name);
}
