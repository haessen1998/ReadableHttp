using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ReadableHttp.App.Maui.Components.Layout;

public sealed class MainLayoutResizeInterop : IAsyncDisposable
{
    internal const string ModulePath = "./Components/Layout/MainLayout.razor.js";
    internal const string InitializeMethod = "initialize";
    internal const string DisposeMethod = "dispose";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public MainLayoutResizeInterop(IJSRuntime js)
    {
        _js = js;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        return _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }

    public async ValueTask InitializeAsync(ElementReference workbench)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(InitializeMethod, workbench);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync(DisposeMethod);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
