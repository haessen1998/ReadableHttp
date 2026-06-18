using ReadableHttp.Core;

namespace ReadableHttp.Execution;

public interface IReadableHttpExecutor
{
    Task<ReadableExchange> SendAsync(
        ReadableRequest request,
        ReadableExecutionContext? context = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ReadableStreamMessage> StreamAsync(
        ReadableRequest request,
        ReadableExecutionContext? context = null,
        ReadableStreamOptions? options = null,
        CancellationToken cancellationToken = default);
}
