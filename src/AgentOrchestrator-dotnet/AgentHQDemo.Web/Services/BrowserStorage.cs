using Microsoft.JSInterop;

namespace AgentHQDemo.Web.Services;

public sealed class ChatEntry
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Status { get; set; } = "";
}

public sealed class BrowserState
{
    public string Model { get; set; } = "";
    public string Theme { get; set; } = "light";
    public List<ChatEntry> Messages { get; set; } = [];
}

public sealed class BrowserStorage(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");

    public async Task<BrowserState?> LoadAsync() =>
        await (await ModuleAsync()).InvokeAsync<BrowserState?>("load");

    public async Task SaveAsync(BrowserState state) =>
        await (await ModuleAsync()).InvokeVoidAsync("save", state);

    public async Task InitializeComposerAsync(string language) =>
        await (await ModuleAsync()).InvokeVoidAsync("initializeComposer", language);

    public async Task SetLanguageAsync(string language) =>
        await (await ModuleAsync()).InvokeVoidAsync("setLanguage", language);

    public async Task ResetComposerAsync() =>
        await (await ModuleAsync()).InvokeVoidAsync("resetComposer");

    public async Task FocusComposerAsync() =>
        await (await ModuleAsync()).InvokeVoidAsync("focusComposer");

    public async Task ScrollAsync(bool force = false) =>
        await (await ModuleAsync()).InvokeVoidAsync("scrollChat", force);

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            await module.DisposeAsync();
        }
    }
}
