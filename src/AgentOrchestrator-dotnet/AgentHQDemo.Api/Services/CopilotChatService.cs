using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace AgentHQDemo.Api.Services;

public sealed class CopilotChatService : IChatService, IAsyncDisposable
{
    private const string RetailServer = "retail-analytics";
    private static readonly string[] RetailTools =
        ["list_transactions", "get_transaction", "list_segments", "get_customer_summary", "predict_segment"];
    private readonly CopilotClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CopilotChatService> _logger;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _started;

    public CopilotChatService(IConfiguration configuration, ILogger<CopilotChatService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(Environment.GetEnvironmentVariable("COPILOT_CLI_PATH") ?? "copilot"),
            Logger = logger
        });
    }

    private async Task EnsureStartedAsync(CancellationToken ct)
    {
        await _startLock.WaitAsync(ct);
        try
        {
            if (_started)
                return;
            await _client.StartAsync(ct);
            _started = true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task<IReadOnlyList<ChatModel>> ListModelsAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await EnsureStartedAsync(timeout.Token);
        var models = await _client.ListModelsAsync(cancellationToken: timeout.Token);
        return models.Select(m => new ChatModel(m.Id, m.Name)).ToArray();
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        var producer = ProduceAsync(request, channel.Writer, timeout.Token);
        try
        {
            await foreach (var delta in channel.Reader.ReadAllAsync(ct))
                yield return delta;
        }
        finally
        {
            await timeout.CancelAsync();
            await producer;
        }
    }

    private async Task ProduceAsync(ChatRequest request, ChannelWriter<string> output, CancellationToken ct)
    {
        try
        {
            await EnsureStartedAsync(ct);
            var models = await ListModelsAsync(ct);
            if (models.Count == 0)
                throw new InvalidOperationException("No Copilot models are available for the signed-in account.");
            var model = request.Model ?? models.FirstOrDefault(m => m.Id == "claude-haiku-4.5")?.Id ?? models[0].Id;
            if (!models.Any(m => m.Id == model))
                throw new InvalidOperationException($"Model \"{model}\" is not available.");
            var serverDll = Path.Combine(AppContext.BaseDirectory, "mcp", "AgentHQDemo.McpServer.dll");
            if (!File.Exists(serverDll))
                throw new FileNotFoundException("Build the solution to make the retail MCP server available.", serverDll);

            await using var session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = model,
                Streaming = true,
                AvailableTools = RetailTools.Select(t => $"mcp:{RetailServer}-{t}").ToList(),
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = """
                        You are a retail analytics assistant. Use the retail-analytics tools for facts
                        about customers, purchases and retention. Never invent database values.
                        Predictions are illustrative rules, not a trained ML model.
                        If a tool fails or is unavailable, clearly say so. Answer in the user's language.
                        """ + "\n" + request.SystemMessage
                },
                McpServers = new Dictionary<string, McpServerConfig>
                {
                    [RetailServer] = new McpStdioServerConfig
                    {
                        Command = "dotnet",
                        Args = [serverDll],
                        Tools = ["*"],
                        Env = new Dictionary<string, string>
                        {
                            ["Retail__DatabasePath"] = _configuration["Retail:DatabasePath"]!
                        }
                    }
                },
                OnPermissionRequest = (request, _) =>
                    Task.FromResult(request is PermissionRequestMcp mcp && mcp.ServerName == RetailServer && mcp.ReadOnly
                        ? PermissionDecision.ApproveOnce()
                        : PermissionDecision.Reject("Only read-only retail MCP tools are permitted."))
            }, ct);
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = session.On<SessionEvent>(evt =>
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        if (!string.IsNullOrEmpty(delta.Data.DeltaContent))
                            output.TryWrite(delta.Data.DeltaContent);
                        break;
                    case AssistantMessageEvent message:
                        _logger.LogInformation("Assistant response complete: {Length} chars", message.Data.Content?.Length ?? 0);
                        break;
                    case ToolExecutionStartEvent tool when !string.IsNullOrEmpty(tool.Data.McpServerName):
                        _logger.LogInformation("MCP tool call: {Server}/{Tool}", tool.Data.McpServerName, tool.Data.ToolName);
                        break;
                    case ToolExecutionCompleteEvent tool:
                        _logger.LogInformation("Tool {ToolCallId} completed: success={Success}", tool.Data.ToolCallId, tool.Data.Success);
                        break;
                    case SessionIdleEvent:
                        done.TrySetResult();
                        break;
                    case SessionErrorEvent error:
                        done.TrySetException(new InvalidOperationException(error.Data.Message));
                        break;
                }
            });
            try
            {
                await session.SendAsync(new MessageOptions { Prompt = request.Prompt }, ct);
                await done.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                using var abortTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await session.AbortAsync(abortTimeout.Token);
                }
                catch (Exception abortError)
                {
                    _logger.LogWarning(abortError, "Could not abort the cancelled Copilot turn");
                }
                throw;
            }
            output.TryComplete();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Copilot chat failed");
            output.TryComplete(exception is OperationCanceledException
                ? new TimeoutException("Chat was cancelled or exceeded the 120-second limit.", exception) : exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        _startLock.Dispose();
    }
}
