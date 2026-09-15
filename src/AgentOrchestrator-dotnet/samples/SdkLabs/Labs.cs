using GitHub.Copilot;
using GitHub.Copilot.Rpc;

#pragma warning disable GHCP001 // SDK 1.0.9 requires the experimental Rpc.PermissionDecision callback result.

namespace SdkLabs;

internal static class Labs
{
    public const string LearnServer = "microsoft-learn";
    public static readonly string[] LearnTools =
        ["microsoft_docs_search", "microsoft_docs_fetch", "microsoft_code_sample_search"];

    public static async Task RunAsync(CopilotClient client, LabOptions options, string model, CancellationToken token)
    {
        switch (options.Command)
        {
            case "tools":
                var toolsConfig = Configure(new SessionConfig(), model, DemoTools.CustomerToolNames);
                toolsConfig.Tools = DemoTools.CustomerTools();
                toolsConfig.OnPermissionRequest = (request, _) => Task.FromResult(
                    request is PermissionRequestCustomTool tool && DemoTools.CustomerToolNames.Contains(tool.ToolName)
                        ? PermissionDecision.ApproveOnce() : PermissionDecision.Reject("Only the two customer demo tools are allowed."));
                await using (var session = await client.CreateSessionAsync(toolsConfig, token))
                {
                    var result = await TurnRunner.RunAsync(session, options.Prompt ??
                        "Call get_customer_purchases and get_customer_categories for C003. Report each purchase, " +
                        "the total and category totals. Then call get_customer_purchases for C999 and report whether it exists. " +
                        "Use only tool results, never invent customers.", token);
                    if (!ToolsSucceeded(result, options.ExpectNoTools))
                        throw new InvalidOperationException(options.ExpectNoTools
                            ? $"Expected zero tool calls, but observed {result.ToolCallCount}."
                            : "No customer tool completed successfully; the tools lab was not demonstrated.");
                    if (options.ExpectNoTools)
                        Console.WriteLine($"Verified no-tool response: tool calls={result.ToolCallCount}.");
                }
                break;
            case "events":
                await using (var session = await client.CreateSessionAsync(
                    Configure(new SessionConfig(), model, []), token))
                {
                    await TurnRunner.RunAsync(session, options.Prompt ??
                        "用中文简短解释：在客户分群应用中，为什么需要流式响应和 Token 用量事件？", token);
                }
                break;
            case "permissions":
                var demo = new PermissionDemo();
                var permissionConfig = Configure(new SessionConfig(), model, [DemoTools.AllowedName, DemoTools.DeniedName]);
                permissionConfig.Tools = demo.Tools();
                permissionConfig.OnPermissionRequest = demo.Handle;
                await using (var session = await client.CreateSessionAsync(permissionConfig, token))
                {
                    try
                    {
                        await TurnRunner.RunAsync(session,
                            "This is a harmless permission-policy test with synthetic data only. " +
                            "Call read_demo_summary once, then attempt read_demo_restricted once. " +
                            "If rejected, respect the decision and do not retry. Report observed outcomes, not assumptions.", token);
                    }
                    finally { demo.Report(); }
                }
                break;
            case "sessions":
                await RunSessionsAsync(client, options, model, token);
                break;
            case "mcp":
                var mcpConfig = Configure(new SessionConfig(), model,
                    LearnTools.Select(name => $"{LearnServer}-{name}").ToArray());
                mcpConfig.McpServers = new Dictionary<string, McpServerConfig>
                {
                    [LearnServer] = new McpHttpServerConfig
                    {
                        Url = "https://learn.microsoft.com/api/mcp",
                        Tools = ["*"],
                        Timeout = 60_000
                    }
                };
                mcpConfig.OnPermissionRequest = (request, _) => Task.FromResult(
                    IsLearnPermission(request)
                        ? PermissionDecision.ApproveOnce()
                        : PermissionDecision.Reject("Only Microsoft Learn's read-only MCP tools are permitted."));
                await using (var session = await client.CreateSessionAsync(mcpConfig, token))
                {
                    var result = await TurnRunner.RunAsync(session, options.Prompt ??
                        "Use the microsoft-learn MCP microsoft_docs_search tool to find official documentation about " +
                        ".NET dependency injection. Summarize it briefly in Chinese and cite the returned learn.microsoft.com URLs. " +
                        "You MUST call the MCP tool; do not answer from memory or use web_fetch.", token);
                    if (!result.McpProof.IsSuccessful)
                        throw new InvalidOperationException(
                            $"MCP verification failed: starts={result.McpProof.StartCount}, successful completions={result.McpProof.SuccessCount}, " +
                            $"failures={result.McpProof.FailureCount}. A text answer or built-in web_fetch is NOT an MCP call. " +
                            "Check CLI compatibility, Learn endpoint access and the MCP tool allowlist.");
                    Console.WriteLine($"Verified Microsoft Learn MCP: {result.McpProof.SuccessCount} successful call(s).");
                }
                break;
        }
    }

    internal static bool ToolsSucceeded(TurnResult result, bool expectNoTools) =>
        !string.IsNullOrWhiteSpace(result.Answer) && (expectNoTools
            ? result.ToolCallCount == 0
            : result.SuccessfulTools.Overlaps(DemoTools.CustomerToolNames));

    internal static T Configure<T>(T config, string model, IList<string> availableTools) where T : SessionConfigBase
    {
        config.Model = model;
        config.Streaming = true;
        config.AvailableTools = availableTools;
        config.EnableConfigDiscovery = false;
        config.EnableFileHooks = false;
        config.EnableSkills = false;
        config.EnableHostGitOperations = false;
        config.EnableOnDemandInstructionDiscovery = false;
        config.SkipCustomInstructions = true;
        config.ManageScheduleEnabled = false;
        config.OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("No host tools are authorized by this lab."));
        return config;
    }

    public static bool IsLearnPermission(PermissionRequest request) =>
        request is PermissionRequestMcp mcp && mcp.ServerName == LearnServer && mcp.ReadOnly &&
        LearnTools.Any(name => mcp.ToolName == name || mcp.ToolName == $"{LearnServer}-{name}");

    private static async Task RunSessionsAsync(CopilotClient client, LabOptions options, string model, CancellationToken token)
    {
        var id = options.ResumeId ?? $"sdklabs-{Guid.NewGuid():N}";
        Console.WriteLine($"Session ID: {id}");
        if (options.ResumeId is null)
        {
            var config = Configure(new SessionConfig { SessionId = id }, model, []);
            await using (var original = await client.CreateSessionAsync(config, token))
            {
                await TurnRunner.RunAsync(original,
                    "For this conversation only, remember that my selected customer segment is exactly 'At Risk'. " +
                    "Acknowledge briefly. Do not use any persistent-memory tool.", token);
            }
            Console.WriteLine("Original session disposed; resuming the stored conversation.");
        }
        else if (await client.GetSessionMetadataAsync(id, token) is null)
        {
            throw new InvalidOperationException($"Lab session '{id}' was not found. Create one with the sessions command first.");
        }

        var resumeConfig = Configure(new ResumeSessionConfig { ContinuePendingWork = false }, model, []);
        await using (var resumed = await client.ResumeSessionAsync(id, resumeConfig, token))
        {
            var result = await TurnRunner.RunAsync(resumed,
                "What exact customer segment did I ask you to remember earlier in this conversation? " +
                "Answer only with that segment label. If you cannot find it, say UNKNOWN.", token);
            if (!result.Answer.Contains("At Risk", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The resumed conversation did not recall the expected segment. Review the saved session and model response.");
        }

        var metadata = await client.GetSessionMetadataAsync(id, token);
        if (metadata is null) throw new InvalidOperationException("The lab session's metadata was not found after resume.");
        Console.WriteLine($"Metadata: id={metadata.SessionId}, created={metadata.StartTime:O}, modified={metadata.ModifiedTime:O}");
        Console.WriteLine($"Cross-process: sessions --resume {id}");
        Console.WriteLine($"Explicit cleanup: sessions --delete {id}");
    }
}
