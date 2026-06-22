namespace ReadableHttp.App.Maui;

public sealed class AppFilePicker
{
    public async Task<string?> PickFolderAsync(string title)
    {
#if WINDOWS
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window is null)
        {
            return null;
        }

        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    public async Task<string?> PickTryFileAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Open API file"
        });

        return result?.FullPath;
    }

    public async Task<string?> PickWorkspaceAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Open workspace.json"
        });

        if (result?.FullPath is null)
        {
            return null;
        }

        return string.Equals(Path.GetFileName(result.FullPath), "workspace.json", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(result.FullPath)
            : null;
    }
}
