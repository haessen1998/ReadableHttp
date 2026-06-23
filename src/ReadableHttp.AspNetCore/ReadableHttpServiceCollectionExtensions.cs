using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadableHttp.Execution;

namespace ReadableHttp.AspNetCore;

public static class ReadableHttpServiceCollectionExtensions
{
    public static IServiceCollection AddReadableHttp(this IServiceCollection services)
    {
        return services.AddReadableHttp(configureClient: null);
    }

    public static IServiceCollection AddReadableHttp(
        this IServiceCollection services,
        Action<HttpClient>? configureClient)
    {
        services.AddHttpClient();
        var builder = services.AddHttpClient(ReadableHttpClientNames.Default);
        if (configureClient is not null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        services.TryAddSingleton<IReadableHttpExecutor>(provider =>
            new ReadableHttpExecutor(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(ReadableHttpClientNames.Default)));
        services.TryAddSingleton<IReadableHttpFactory, ReadableHttpFactory>();
        return services;
    }
}
