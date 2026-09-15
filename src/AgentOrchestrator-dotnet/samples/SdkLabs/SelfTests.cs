using GitHub.Copilot;

namespace SdkLabs;

internal static class SelfTests
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"Self-test failed: {name}");
            checks++;
        }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (ArgumentException) { checks++; return; }
            throw new InvalidOperationException($"Self-test failed: {name} should reject.");
        }

        var customer = DemoTools.GetPurchases("C003");
        Check(customer.Found && customer.Purchases.Length == 2 && customer.Total == 1700m, "C003 fixture");
        Check(customer.Purchases.Select(p => p.Category).SequenceEqual(["Electronics", "Fashion"]), "fixture categories");
        var categories = DemoTools.GetCategories("C003");
        Check(categories.Found && categories.Categories.Sum(c => c.Total) == 1700m, "category aggregation");
        Check(!DemoTools.GetPurchases("C999").Found && DemoTools.GetPurchases("C999").Error!.Contains("not found"), "unknown customer");
        Check(!DemoTools.GetCategories("C999").Found && DemoTools.GetCategories("C999").Categories.Length == 0, "unknown categories");
        var tools = DemoTools.CustomerTools();
        Check(tools.Select(t => t.Name).SequenceEqual(DemoTools.CustomerToolNames), "tool allowlist matches declarations");
        Check(tools.All(t => !string.IsNullOrWhiteSpace(t.Description) &&
            t.JsonSchema.ToString().Contains("Customer identifier")), "method and parameter descriptions");
        Check(LabOptions.Parse([]).Command == "help", "no-args help");
        Check(LabOptions.Parse(["mcp", "--help"]).Command == "help", "command help");
        Check(LabOptions.Parse(["tools", "--prompt", "C999", "--timeout", "30"]).TimeoutSeconds == 30, "options");
        Check(LabOptions.Parse(["tools", "--expect-no-tools"]).ExpectNoTools, "no-tools flag");
        Reject(() => LabOptions.Parse(["events", "--expect-no-tools"]), "misplaced no-tools flag");
        Reject(() => LabOptions.Parse(["tools", "--expect-no-tools", "--expect-no-tools"]), "duplicate no-tools flag");
        var noTools = new TurnResult("A definition.", [], new(), 0);
        var usedTool = new TurnResult("Customer data.", [DemoTools.PurchasesName], new(), 1);
        Check(Labs.ToolsSucceeded(noTools, true), "definition without tool calls succeeds when requested");
        Check(!Labs.ToolsSucceeded(noTools, false), "default still requires a customer tool");
        Check(!Labs.ToolsSucceeded(usedTool, true), "actual tool call violates no-tools expectation");
        Check(!Labs.ToolsSucceeded(noTools with { ToolCallCount = 1 }, true), "failed tool attempt also violates no-tools expectation");
        Check(Labs.ToolsSucceeded(usedTool, false), "default accepts successful customer tool");
        Check(!Labs.ToolsSucceeded(noTools with { Answer = "" }, true), "empty reply fails no-tools expectation");
        Check(LabOptions.SelectModel(["other", "claude-haiku-4.5"], null) == "claude-haiku-4.5", "preferred model");
        Check(LabOptions.SelectModel(["other"], null) == "other", "fallback model");
        Check(LabOptions.SelectModel(["other", "chosen"], "chosen") == "chosen", "explicit model");
        Reject(() => LabOptions.SelectModel(["other"], "missing"), "unknown model");
        Reject(() => LabOptions.Parse(["sessions", "--delete", "personal-session"]), "personal deletion");
        Reject(() => LabOptions.Parse(["sessions", "--delete", "sdklabs-../other"]), "path traversal");
        Reject(() => LabOptions.Parse(["sessions", "--resume", "sdklabs-a", "--delete", "sdklabs-b"]), "conflicting options");
        Reject(() => LabOptions.Parse(["events", "--resume", "sdklabs-a"]), "misplaced resume");
        Reject(() => LabOptions.Parse(["sessions", "--prompt", "ignored"]), "misplaced prompt");
        Reject(() => LabOptions.Parse(["events", "--timeout", "0"]), "invalid timeout");
        Reject(() => LabOptions.Parse(["tools", "--model"]), "missing option value");
        Reject(() => LabOptions.Parse(["tools", "--model", "a", "--model", "b"]), "duplicate option");
        Check(LabOptions.Parse(["sessions", "--delete", "sdklabs-a"]).DeleteId == "sdklabs-a", "explicit lab deletion");

        PermissionRequestCustomTool Request(string name) => new() { ToolName = name, ToolDescription = "Synthetic" };
        Check(PermissionDemo.IsAllowed(Request(DemoTools.AllowedName)), "allow safe summary");
        Check(!PermissionDemo.IsAllowed(Request(DemoTools.DeniedName)), "deny restricted placeholder");
        Check(!PermissionDemo.IsAllowed(Request("bash")), "deny unrelated tool");
        var config = Labs.Configure(new ResumeSessionConfig(), "test", []);
        Check(config.AvailableTools!.Count == 0 && config.EnableConfigDiscovery == false &&
            config.EnableFileHooks == false && config.EnableSkills == false, "resume safety configuration");

        var proof = new McpEvidence();
        proof.Start("web", null, "web_fetch");
        proof.Complete("web", true);
        Check(!proof.IsSuccessful, "web_fetch is not MCP");
        proof.Start("wrong", "unrelated-server", Labs.LearnTools[0]);
        proof.Complete("wrong", true);
        Check(!proof.IsSuccessful, "unrelated MCP server rejected");
        proof.Start("learn", Labs.LearnServer, Labs.LearnTools[0]);
        Check(!proof.IsSuccessful, "start alone is insufficient");
        proof.Complete("learn", true);
        Check(proof.IsSuccessful, "paired successful MCP execution");
        proof.Start("failure", Labs.LearnServer, Labs.LearnTools[0]);
        proof.Complete("failure", false);
        Check(!proof.IsSuccessful, "MCP failure cannot claim success");
        var permission = new PermissionRequestMcp
        {
            ServerName = Labs.LearnServer, ToolName = Labs.LearnTools[0],
            ToolTitle = "Search", ReadOnly = true
        };
        Check(Labs.IsLearnPermission(permission), "read-only Learn permission");
        permission.ReadOnly = false;
        Check(!Labs.IsLearnPermission(permission), "non-read-only permission rejected");
        Console.WriteLine($"PASS: {checks} offline self-tests. No CLI process or AI request was started.");
    }
}
