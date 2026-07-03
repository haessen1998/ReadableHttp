namespace ReadableHttp.App.Maui;

public enum AppCommand
{
    NewRequest,
    PasteImport,
    ReloadWorkspace,
    OpenSettings,
    ToggleExplorer,
    ToggleInspector
}

public sealed class AppCommandService
{
    public event Func<AppCommand, Task>? CommandRequested;

    public Task RequestAsync(AppCommand command)
    {
        return CommandRequested?.Invoke(command) ?? Task.CompletedTask;
    }
}
