using Microsoft.Extensions.DependencyInjection;
using ReadableHttp.Execution;

namespace ReadableHttp.AspNetCore;

public static class ReadableHttpServiceCollectionExtensions
{
    public static IServiceCollection AddReadableHttp(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IReadableHttpExecutor>(provider =>
            new ReadableHttpExecutor(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient()));
        services.AddSingleton<IReadableHttpFactory, ReadableHttpFactory>();
        return services;
    }
}
