using System.Diagnostics;
using GitHub.Copilot;

namespace SdkLabs;

internal sealed class McpEvidence
{
    private readonly HashSet<string> started = [];
    private readonly HashSet<string> succeeded = [];
    private readonly HashSet<string> failed = [];
    public int StartCount => started.Count;
    public int SuccessCount => succeeded.Count;
    public int FailureCount => failed.Count;
    public bool IsSuccessful => succeeded.Count > 0 && failed.Count == 0 && started.SetEquals(succeeded);

    public void Start(string callId, string? serverName, string? toolName)
    {
        if (serverName == Labs.LearnServer && Labs.LearnTools.Contains(toolName))
            started.Add(callId);
    }

    public void Complete(string callId, bool success)
    {
        if (!started.Contains(callId)) return;
        if (success) succeeded.Add(callId);
        else failed.Add(callId);
    }
}

internal sealed record TurnResult(string Answer, HashSet<string> SuccessfulTools, McpEvidence McpProof, int ToolCallCount);

internal static class TurnRunner
{
    public static async Task<TurnResult> RunAsync(CopilotSession session, string prompt, CancellationToken token)
    {
        var idle = new TaskCompletionSource<TurnResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new Stopwatch();
        var firstTokenMs = (double?)null;
        var deltas = 0;
        var usageEvents = 0;
        long inputTokens = 0, outputTokens = 0;
        var answer = "";
        var toolNames = new Dictionary<string, string>();
        var successfulTools = new HashSet<string>(StringComparer.Ordinal);
        var mcp = new McpEvidence();

        using var subscription = session.On<SessionEvent>(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    deltas++;
                    if (delta.Data.DeltaContent.Length > 0)
                        firstTokenMs ??= clock.Elapsed.TotalMilliseconds;
                    Console.Write(delta.Data.DeltaContent);
                    break;
                case AssistantMessageEvent message:
                    answer = message.Data.Content;
                    if (deltas == 0) Console.WriteLine(answer);
                    break;
                case AssistantUsageEvent usage:
                    usageEvents++;
                    inputTokens += usage.Data.InputTokens ?? 0;
                    outputTokens += usage.Data.OutputTokens ?? 0;
                    break;
                case ToolExecutionStartEvent start:
                    toolNames[start.Data.ToolCallId] = start.Data.ToolName;
                    mcp.Start(start.Data.ToolCallId, start.Data.McpServerName, start.Data.McpToolName);
                    Console.WriteLine($"\n[tool.start] {start.Data.ToolName}; mcpServer={start.Data.McpServerName ?? "(none)"}");
                    break;
                case ToolExecutionCompleteEvent complete:
                    var success = complete.Data.Success && complete.Data.Error is null;
                    mcp.Complete(complete.Data.ToolCallId, success);
                    if (success && toolNames.TryGetValue(complete.Data.ToolCallId, out var name))
                        successfulTools.Add(name);
                    Console.WriteLine($"\n[tool.complete] id={complete.Data.ToolCallId}; success={success}");
                    if (!success) Console.Error.WriteLine($"Tool failed: {complete.Data.Error?.Message ?? "no error details"}");
                    break;
                case SessionErrorEvent error:
                    idle.TrySetException(new InvalidOperationException($"Session error: {error.Data.Message}"));
                    break;
                case SessionIdleEvent:
                    clock.Stop();
                    Console.WriteLine($"\n[idle] deltas={deltas}; usage events={usageEvents}; input tokens={inputTokens}; output tokens={outputTokens}");
                    Console.WriteLine($"First nonempty text delta: {(firstTokenMs is null ? "not observed" : $"{firstTokenMs:F0} ms")}; total={clock.Elapsed.TotalMilliseconds:F0} ms");
                    if (usageEvents == 0) Console.WriteLine("No usage event observed; token counts above are not a billing measurement.");
                    idle.TrySetResult(new(answer, successfulTools, mcp, toolNames.Count));
                    break;
            }
        });

        clock.Start();
        try
        {
            await session.SendAsync(new MessageOptions { Prompt = prompt }, token);
            var result = await idle.Task.WaitAsync(token);
            if (string.IsNullOrWhiteSpace(result.Answer))
                throw new InvalidOperationException("Session became idle without an assistant answer.");
            return result;
        }
        catch
        {
            // Cancel the runtime turn as well as our wait; cancellation of SendAsync alone is not an abort.
            using var abortTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await session.AbortAsync(abortTimeout.Token); }
            catch (Exception abortError) { Console.Error.WriteLine($"Abort: {abortError.Message}"); }
            throw;
        }
    }
}
