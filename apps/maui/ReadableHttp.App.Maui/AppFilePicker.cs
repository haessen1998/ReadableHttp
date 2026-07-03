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
#elif MACCATALYST
        return await MacCatalystFolderPicker.PickFolderAsync(title);
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

#if MACCATALYST
    private sealed class MacCatalystFolderPicker : UIKit.UIDocumentPickerDelegate
    {
        private readonly TaskCompletionSource<string?> _completionSource = new();

        private MacCatalystFolderPicker()
        {
        }

        public static Task<string?> PickFolderAsync(string title)
        {
            var presenter = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController();
            if (presenter is null)
            {
                return Task.FromResult<string?>(null);
            }

            var folderPicker = new MacCatalystFolderPicker();
            var picker = new UIKit.UIDocumentPickerViewController([UniformTypeIdentifiers.UTTypes.Folder], false)
            {
                AllowsMultipleSelection = false,
                Delegate = folderPicker,
                Title = title
            };

            presenter.PresentViewController(picker, true, null);
            return folderPicker._completionSource.Task;
        }

        public override void DidPickDocument(UIKit.UIDocumentPickerViewController controller, Foundation.NSUrl url)
        {
            Complete(url);
        }

        public override void DidPickDocument(UIKit.UIDocumentPickerViewController controller, Foundation.NSUrl[] urls)
        {
            Complete(urls.FirstOrDefault());
        }

        public override void WasCancelled(UIKit.UIDocumentPickerViewController controller)
        {
            _completionSource.TrySetResult(null);
        }

        private void Complete(Foundation.NSUrl? url)
        {
            if (url is null)
            {
                _completionSource.TrySetResult(null);
                return;
            }

            url.StartAccessingSecurityScopedResource();
            _completionSource.TrySetResult(url.Path);
        }
    }
#endif
}
