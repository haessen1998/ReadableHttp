using FluentHttp.Json;

namespace FluentHttpFactory;

public interface IFluentHttpFactory
{
    HttpClient Create(string name);

    FluentJsonHttpClient CreateJson(string name);

    FluentFormHttpClient CreateForm(string name);
}
