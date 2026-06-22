namespace ReadableHttp.App.Maui;

public sealed class AppSettings
{
    public string WorkspacePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ReadableHttp Workspace");

    public string TryFilePath { get; set; } = string.Empty;

    public string WorkspaceHistory { get; set; } = string.Empty;

    public string Language { get; set; } = "system";

    public string ThemeMode { get; set; } = "system";

    public bool DarkMode { get; set; }

    public string ProxyMode { get; set; } = "system";

    public string CustomProxyUrl { get; set; } = string.Empty;

    public string CustomProxyUsername { get; set; } = string.Empty;

    public string CustomProxyPassword { get; set; } = string.Empty;

    public bool UseSystemProxy { get; set; } = true;

    public bool DevToolsEnabled { get; set; } = true;
}
