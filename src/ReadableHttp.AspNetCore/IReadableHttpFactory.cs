using ReadableHttp.Execution;

namespace ReadableHttp.AspNetCore;

public interface IReadableHttpFactory
{
    HttpClient CreateClient();

    HttpClient CreateClient(string name);

    IReadableHttpExecutor CreateExecutor();

    IReadableHttpExecutor CreateExecutor(string name);
}
