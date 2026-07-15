using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ReadableHttp.App.Maui.Components.ApiClient;

public sealed class ExplorerSidebarMenuInterop : IAsyncDisposable
{
    internal const string ModulePath = "./Components/ApiClient/ExplorerSidebar.razor.js";
    internal const string PositionOpenMenuMethod = "positionOpenMenu";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public ExplorerSidebarMenuInterop(IJSRuntime js)
    {
        _js = js;
    }

    public async ValueTask PositionOpenMenuAsync(ElementReference sidebar)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(PositionOpenMenuMethod, sidebar);
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        return _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
