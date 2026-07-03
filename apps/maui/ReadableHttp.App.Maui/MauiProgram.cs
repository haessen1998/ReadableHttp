using Microsoft.Extensions.Logging;
using ReadableHttp.AI;
using ReadableHttp.AI.MAF;
using ReadableHttp.App.Maui.Components.ApiClient;

namespace ReadableHttp.App.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("Segoe UI.ttf", "SegoeUI");
                fonts.AddFont("SegMDL2.ttf", "SegMDL2");
                fonts.AddFont("Segoe Fluent Icons.ttf", "SegoeFluentIcons");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<AppCommandService>();
        builder.Services.AddSingleton<AppSettingsStore>();
        builder.Services.AddSingleton<AppFilePicker>();
        builder.Services.AddSingleton<ApiClientWorkspaceState>();
        builder.Services.AddSingleton<IReadableHttpAiToolRegistry, ReadableHttpMafToolRegistry>();
        builder.Services.AddSingleton<IReadableHttpAiConfirmationPolicy, ReadableHttpMafConfirmationPolicy>();
        builder.Services.AddSingleton<IReadableHttpAiAgent, ReadableHttpMafAgent>();
#if WINDOWS
        builder.Services.AddSingleton<WindowsTrayIconService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
