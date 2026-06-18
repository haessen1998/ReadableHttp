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
            Language = Preferences.Default.Get(Prefix + nameof(AppSettings.Language), defaults.Language),
            DarkMode = Preferences.Default.Get(Prefix + nameof(AppSettings.DarkMode), defaults.DarkMode),
            UseSystemProxy = Preferences.Default.Get(Prefix + nameof(AppSettings.UseSystemProxy), defaults.UseSystemProxy),
            DevToolsEnabled = Preferences.Default.Get(Prefix + nameof(AppSettings.DevToolsEnabled), defaults.DevToolsEnabled)
        };
    }

    public void Save(AppSettings settings)
    {
        Preferences.Default.Set(Prefix + nameof(AppSettings.WorkspacePath), settings.WorkspacePath);
        Preferences.Default.Set(Prefix + nameof(AppSettings.TryFilePath), settings.TryFilePath);
        Preferences.Default.Set(Prefix + nameof(AppSettings.Language), settings.Language);
        Preferences.Default.Set(Prefix + nameof(AppSettings.DarkMode), settings.DarkMode);
        Preferences.Default.Set(Prefix + nameof(AppSettings.UseSystemProxy), settings.UseSystemProxy);
        Preferences.Default.Set(Prefix + nameof(AppSettings.DevToolsEnabled), settings.DevToolsEnabled);
    }
}
