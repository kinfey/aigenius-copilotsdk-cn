using System.Text;
using AgentHQDemo.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AgentHQDemo.Web.Pages;

public partial class Home
{
    private const string SafetyMessage = """
        当问题涉及客户、订单、消费、留存或 KPI 时，优先使用可用的只读零售 MCP 工具获取事实。
        不得编造数据、工具调用结果或未提供的指标。缺少数据或工具不可用时明确说明限制。
        说明数据范围、时间窗口、指标口径与必要假设；区分事实、推断和建议。
        不执行写入或修改业务数据的操作。不要泄露不必要的个人信息。
        每次请求可能是独立会话，不假设拥有浏览器中的历史上下文。
        """;

    private string language = "zh-CN";
    private ChatLocale Text => ChatLocale.For(language);
    private string ModelStatus => loadingModels ? Text.Connecting
        : modelError is not null ? $"{Text.ModelFailure} {modelError}"
        : Text.ModelsAvailable(models.Count);
    private BrowserState state = new();
    private List<ChatModel> models = [];
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? requestCancellation;
    private ChatEntry? activeReply;
    private string prompt = "";
    private string? modelError;
    private string? chatError;
    private string? storageError;
    private bool busy;
    private bool loadingModels = true;
    private bool initialized;
    private bool disposed;
    private bool scrollAfterRender;
    private bool forceScroll;
    private bool ModelsReady => initialized && !loadingModels && models.Any(model => model.Id == state.Model);
    private bool CanSend => ModelsReady && !busy && !string.IsNullOrWhiteSpace(prompt);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                state = await Storage.LoadAsync() ?? new BrowserState();
            }
            catch (Exception)
            {
                storageError = "无法读取浏览器存储。对话仍可使用，但历史记录可能无法保存。";
            }
            await Storage.InitializeComposerAsync(language);
            initialized = true;
            await LoadModelsAsync();
            scrollAfterRender = state.Messages.Count > 0;
            forceScroll = scrollAfterRender;
            if (!disposed) StateHasChanged();
        }
        if (scrollAfterRender && !disposed)
        {
            scrollAfterRender = false;
            try { await Storage.ScrollAsync(forceScroll); }
            catch (Microsoft.JSInterop.JSException) { /* Scrolling is nonessential. */ }
            forceScroll = false;
        }
    }

    private async Task LoadModelsAsync()
    {
        loadingModels = true;
        modelError = null;
        try
        {
            models = await Api.GetModelsAsync(lifetime.Token);
            if (models.Count == 0)
            {
                state.Model = "";
                modelError = "后端没有返回任何可用模型。";
            }
            else if (!models.Any(model => model.Id == state.Model))
            {
                state.Model = models.FirstOrDefault(model => model.Id == "gpt-6-astra")?.Id ?? models[0].Id;
            }
            await PersistAsync();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            models = [];
            state.Model = "";
            modelError = ex is OperationCanceledException ? "获取模型超时（30 秒）。" : ex.Message;
        }
        finally
        {
            loadingModels = false;
        }
    }

    private async Task UseSuggestionAsync(string suggestion)
    {
        if (!ModelsReady || busy) return;
        prompt = suggestion;
        await SendAsync();
    }

    private async Task ChangeLanguageAsync(ChangeEventArgs args)
    {
        language = args.Value?.ToString() is "zh-TW" or "en" ? args.Value.ToString()! : "zh-CN";
        await Storage.SetLanguageAsync(language);
    }

    private string MessageTime(ChatEntry entry) => entry.CreatedAt.ToLocalTime().ToString(
        language == "en" ? "hh:mm tt" : "HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private async Task ChangeModelAsync(ChangeEventArgs args)
    {
        if (busy) return;
        var selected = args.Value?.ToString();
        if (models.Any(model => model.Id == selected))
        {
            state.Model = selected!;
            await PersistAsync();
        }
    }

    private async Task SendAsync()
    {
        if (!CanSend) return;
        busy = true;
        chatError = null;
        var question = prompt.Trim();
        var requestLocale = Text;
        prompt = "";
        state.Messages.Add(new ChatEntry { Role = "user", Content = question });
        var reply = new ChatEntry { Role = "assistant", Status = "等待模型响应…" };
        activeReply = reply;
        state.Messages.Add(reply);
        if (state.Messages.Count > 100) state.Messages.RemoveRange(0, state.Messages.Count - 100);
        scrollAfterRender = forceScroll = true;
        requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var cancellationToken = requestCancellation.Token;
        var pending = new StringBuilder();
        using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var renderTask = RenderStreamAsync(reply, pending, renderCancellation.Token);
        try
        {
            StateHasChanged();
            await Storage.ResetComposerAsync();
            await foreach (var content in Api.StreamAsync(new ChatRequest(question, state.Model, requestLocale.SystemMessage + "\n" + SafetyMessage), cancellationToken))
            {
                pending.Append(content);
                if (pending.Length + reply.Content.Length > 100_000)
                    throw new InvalidOperationException("回复超过显示上限，已停止。请缩小问题范围后重试。");
            }
            reply.Status = "分析完成";
        }
        catch (OperationCanceledException)
        {
            reply.Status = "已停止 · 已保留收到的内容";
        }
        catch (Exception ex)
        {
            reply.Status = "响应未完成";
            chatError = $"{requestLocale.Error} {ex.Message}";
            prompt = question;
        }
        finally
        {
            renderCancellation.Cancel();
            await renderTask;
            FlushPending(reply, pending);
            busy = false;
            activeReply = null;
            requestCancellation.Dispose();
            requestCancellation = null;
            if (!disposed)
            {
                await PersistAsync();
                scrollAfterRender = true;
                StateHasChanged();
                await Storage.FocusComposerAsync();
            }
        }
    }

    private async Task RenderStreamAsync(ChatEntry reply, StringBuilder pending, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(75));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (pending.Length == 0) continue;
                await InvokeAsync(() =>
                {
                    FlushPending(reply, pending);
                    reply.Status = "正在生成…";
                    scrollAfterRender = true;
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    private static void FlushPending(ChatEntry reply, StringBuilder pending)
    {
        if (pending.Length == 0) return;
        reply.Content += pending.ToString();
        pending.Clear();
    }

    private void Stop() => requestCancellation?.Cancel();

    private async Task PersistAsync()
    {
        try
        {
            await Storage.SaveAsync(state);
            storageError = null;
        }
        catch (Exception)
        {
            storageError = "无法保存浏览器历史（存储被禁用或空间不足）。当前对话仍可使用，刷新后可能丢失。";
        }
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
