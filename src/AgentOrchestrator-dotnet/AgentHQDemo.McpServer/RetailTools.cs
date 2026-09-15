using System.ComponentModel;
using AgentHQDemo.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace AgentHQDemo.McpServer;

[McpServerToolType]
public static class RetailTools
{
    [McpServerTool(Name = "list_transactions", ReadOnly = true, Destructive = false)]
    [Description("Lists retail purchases, optionally filtered by customer ID. Includes amounts, categories and segment IDs.")]
    public static Task<List<RetailTransaction>> ListTransactions(
        RetailAnalyticsService retail,
        [Description("Customer ID such as C003; omit to list all purchases.")] string? customerId = null,
        CancellationToken cancellationToken = default) =>
        retail.GetTransactionsAsync(customerId, cancellationToken);

    [McpServerTool(Name = "get_transaction", ReadOnly = true, Destructive = false)]
    [Description("Retrieves one retail transaction by its numeric transaction ID.")]
    public static async Task<RetailTransaction> GetTransaction(
        RetailAnalyticsService retail, [Description("Transaction ID, for example 7.")] int id,
        CancellationToken cancellationToken) =>
        await retail.GetTransactionAsync(id, cancellationToken)
            ?? throw new McpException($"Transaction {id} was not found.");

    [McpServerTool(Name = "list_segments", ReadOnly = true, Destructive = false)]
    [Description("Lists customer segments and their retention rates as percentages. Use this to compare retention.")]
    public static Task<List<CustomerSegment>> ListSegments(
        RetailAnalyticsService retail, CancellationToken cancellationToken) =>
        retail.GetSegmentsAsync(cancellationToken);

    [McpServerTool(Name = "get_customer_summary", ReadOnly = true, Destructive = false)]
    [Description("Summarises a customer's total spend, average spend, transaction count and purchased categories.")]
    public static async Task<CustomerSummaryDto> GetCustomerSummary(
        RetailAnalyticsService retail, [Description("Customer ID such as C003.")] string customerId,
        CancellationToken cancellationToken) =>
        await retail.GetCustomerSummaryAsync(customerId, cancellationToken)
            ?? throw new McpException($"No transactions found for customer {customerId}.");

    [McpServerTool(Name = "predict_segment", ReadOnly = true, Destructive = false)]
    [Description("Returns a rule-based demonstration segment prediction and supporting features for a retail customer.")]
    public static async Task<SegmentPredictionDto> PredictSegment(
        RetailAnalyticsService retail, [Description("Customer ID such as C003.")] string customerId,
        CancellationToken cancellationToken) =>
        await retail.PredictSegmentAsync(customerId, cancellationToken)
            ?? throw new McpException($"No transactions found for customer {customerId}.");
}
