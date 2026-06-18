namespace ReadableHttp.App.Maui;

public sealed class AppFilePicker
{
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
            : result.FullPath;
    }
}
