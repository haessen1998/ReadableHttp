namespace ReadableHttp.App.Maui;

public sealed class AppSettingsStore
{
    private const string Prefix = "ReadableHttp.App.";

    public AppSettings Load()
    {
        var defaults = new AppSettings();
        return new AppSettings
        {
            WorkspacePath = Preferences.Default.Get(Prefix + nameof(AppSettings.WorkspacePath), defaults.WorkspacePath),
            TryFilePath = Preferences.Default.Get(Prefix + nameof(AppSettings.TryFilePath), defaults.TryFilePath),
            WorkspaceHistory = Preferences.Default.Get(Prefix + nameof(AppSettings.WorkspaceHistory), defaults.WorkspaceHistory),
            Language = Preferences.Default.Get(Prefix + nameof(AppSettings.Language), defaults.Language),
            ThemeMode = Preferences.Default.Get(Prefix + nameof(AppSettings.ThemeMode), defaults.ThemeMode),
            DarkMode = Preferences.Default.Get(Prefix + nameof(AppSettings.DarkMode), defaults.DarkMode),
            ProxyMode = Preferences.Default.Get(
                Prefix + nameof(AppSettings.ProxyMode),
                Preferences.Default.Get(Prefix + nameof(AppSettings.UseSystemProxy), defaults.UseSystemProxy) ? "system" : "none"),
            CustomProxyUrl = Preferences.Default.Get(Prefix + nameof(AppSettings.CustomProxyUrl), defaults.CustomProxyUrl),
            CustomProxyUsername = Preferences.Default.Get(Prefix + nameof(AppSettings.CustomProxyUsername), defaults.CustomProxyUsername),
            CustomProxyPassword = Preferences.Default.Get(Prefix + nameof(AppSettings.CustomProxyPassword), defaults.CustomProxyPassword),
            UseSystemProxy = Preferences.Default.Get(Prefix + nameof(AppSettings.UseSystemProxy), defaults.UseSystemProxy),
            DevToolsEnabled = Preferences.Default.Get(Prefix + nameof(AppSettings.DevToolsEnabled), defaults.DevToolsEnabled)
        };
    }

    public void Save(AppSettings settings)
    {
        Preferences.Default.Set(Prefix + nameof(AppSettings.WorkspacePath), settings.WorkspacePath);
        Preferences.Default.Set(Prefix + nameof(AppSettings.TryFilePath), settings.TryFilePath);
        Preferences.Default.Set(Prefix + nameof(AppSettings.WorkspaceHistory), settings.WorkspaceHistory);
        Preferences.Default.Set(Prefix + nameof(AppSettings.Language), settings.Language);
        Preferences.Default.Set(Prefix + nameof(AppSettings.ThemeMode), settings.ThemeMode);
        Preferences.Default.Set(Prefix + nameof(AppSettings.DarkMode), settings.DarkMode);
        Preferences.Default.Set(Prefix + nameof(AppSettings.ProxyMode), settings.ProxyMode);
        Preferences.Default.Set(Prefix + nameof(AppSettings.CustomProxyUrl), settings.CustomProxyUrl);
        Preferences.Default.Set(Prefix + nameof(AppSettings.CustomProxyUsername), settings.CustomProxyUsername);
        Preferences.Default.Set(Prefix + nameof(AppSettings.CustomProxyPassword), settings.CustomProxyPassword);
        Preferences.Default.Set(Prefix + nameof(AppSettings.UseSystemProxy), settings.UseSystemProxy);
        Preferences.Default.Set(Prefix + nameof(AppSettings.DevToolsEnabled), settings.DevToolsEnabled);
    }
}
