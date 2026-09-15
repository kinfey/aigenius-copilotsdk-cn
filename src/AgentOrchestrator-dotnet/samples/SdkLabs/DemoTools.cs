using System.ComponentModel;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;

#pragma warning disable GHCP001 // SDK 1.0.9 requires the experimental Rpc.PermissionDecision callback result.

namespace SdkLabs;

internal sealed record Purchase(string Product, string Category, decimal Amount);
internal sealed record PurchaseResult(string CustomerId, bool Found, string? Error, Purchase[] Purchases, decimal Total);
internal sealed record CategoryTotal(string Category, decimal Total);
internal sealed record CategoryResult(string CustomerId, bool Found, string? Error, CategoryTotal[] Categories);

internal static class DemoTools
{
    public const string PurchasesName = "get_customer_purchases";
    public const string CategoriesName = "get_customer_categories";
    public const string AllowedName = "read_demo_summary";
    public const string DeniedName = "read_demo_restricted";
    public static readonly string[] CustomerToolNames = [PurchasesName, CategoriesName];

    public static ICollection<AIFunctionDeclaration> CustomerTools() =>
    [
        CopilotTool.DefineTool(GetPurchases, factoryOptions: new() { Name = PurchasesName }),
        CopilotTool.DefineTool(GetCategories, factoryOptions: new() { Name = CategoriesName })
    ];

    [Description("Read the synthetic purchase history for a customer. C003 has two purchases; unknown IDs return an explicit not-found result.")]
    public static PurchaseResult GetPurchases(
        [Description("Customer identifier, for example C003. Never invent an identifier.")] string customerId)
    {
        if (!string.Equals(customerId, "C003", StringComparison.OrdinalIgnoreCase))
            return new(customerId, false, $"Customer {customerId} not found.", [], 0);
        Purchase[] purchases =
        [
            new("Headphones", "Electronics", 1200m),
            new("Jacket", "Fashion", 500m)
        ];
        return new("C003", true, null, purchases, purchases.Sum(p => p.Amount));
    }

    [Description("Summarize synthetic purchases by category. This is the second-tool bonus exercise; unknown customers return not found.")]
    public static CategoryResult GetCategories(
        [Description("Customer identifier to summarize, for example C003.")] string customerId)
    {
        var purchases = GetPurchases(customerId);
        return new(purchases.CustomerId, purchases.Found, purchases.Error,
            purchases.Purchases.GroupBy(p => p.Category)
                .Select(group => new CategoryTotal(group.Key, group.Sum(p => p.Amount))).ToArray());
    }
}

internal sealed class PermissionDemo
{
    private int callbacks;
    private int approved;
    private int rejected;
    private int allowedExecutions;
    private int restrictedExecutions;

    public ICollection<AIFunctionDeclaration> Tools() =>
    [
        CopilotTool.DefineTool(ReadSummary, factoryOptions: new() { Name = DemoTools.AllowedName }),
        CopilotTool.DefineTool(ReadRestricted, factoryOptions: new() { Name = DemoTools.DeniedName })
    ];

    public Task<PermissionDecision> Handle(PermissionRequest request, PermissionInvocation _)
    {
        Interlocked.Increment(ref callbacks);
        var allow = IsAllowed(request);
        if (allow) Interlocked.Increment(ref approved);
        else Interlocked.Increment(ref rejected);
        Console.WriteLine($"[permission] kind={request.Kind}; decision={(allow ? "approve once" : "reject")}");
        return Task.FromResult(allow
            ? PermissionDecision.ApproveOnce()
            : PermissionDecision.Reject("Only read_demo_summary is permitted in this exercise."));
    }

    public static bool IsAllowed(PermissionRequest request) =>
        request is PermissionRequestCustomTool { ToolName: DemoTools.AllowedName };

    [Description("Read an allowed, public synthetic customer summary. No file, network or shell access.")]
    public string ReadSummary()
    {
        Interlocked.Increment(ref allowedExecutions);
        return "Synthetic customer C003: purchase total 1700.";
    }

    [Description("Request a restricted synthetic summary to demonstrate rejection. No real private data or side effects exist in this tool.")]
    public string ReadRestricted()
    {
        Interlocked.Increment(ref restrictedExecutions);
        return "DEMO ONLY: harmless placeholder. No private data was accessed.";
    }

    public void Report()
    {
        Console.WriteLine($"Permission callbacks={callbacks}; approved={approved}; rejected={rejected}; " +
            $"summary executions={allowedExecutions}; restricted placeholder executions={restrictedExecutions}");
        if (callbacks == 0)
            Console.WriteLine("No permission callback observed: this run does NOT demonstrate enforcement. Runtime behavior may auto-approve custom tools.");
        if (restrictedExecutions > 0)
            Console.WriteLine("The restricted placeholder executed: rejection was NOT enforced for that call. Never replace it with a sensitive operation.");
    }
}
