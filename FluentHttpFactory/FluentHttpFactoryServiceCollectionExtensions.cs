using Microsoft.Extensions.DependencyInjection;

namespace FluentHttpFactory;

public static class FluentHttpFactoryServiceCollectionExtensions
{
    public static IServiceCollection AddFluentHttpFactory(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IFluentHttpFactory, FluentHttpFactory>();
        return services;
    }
}
