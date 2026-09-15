using Microsoft.EntityFrameworkCore;

namespace AgentHQDemo.Core;

public sealed class RetailAnalyticsService(RetailDbContext db)
{
    public Task<List<RetailTransaction>> GetTransactionsAsync(string? customerId = null, CancellationToken ct = default) =>
        db.Transactions.AsNoTracking()
            .Where(t => customerId == null || t.CustomerId == customerId)
            .OrderBy(t => t.Id).ToListAsync(ct);

    public Task<RetailTransaction?> GetTransactionAsync(int id, CancellationToken ct = default) =>
        db.Transactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<CustomerSegment>> GetSegmentsAsync(CancellationToken ct = default) =>
        db.Segments.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);

    public Task<CustomerSegment?> GetSegmentAsync(int id, CancellationToken ct = default) =>
        db.Segments.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(string customerId, CancellationToken ct = default)
    {
        var transactions = await GetTransactionsAsync(customerId, ct);
        if (transactions.Count == 0)
            return null;
        return new(customerId, transactions.Sum(t => t.Amount), transactions.Average(t => t.Amount),
            transactions.Count, transactions.Select(t => t.Category).Distinct().Order().ToArray());
    }

    public async Task<SegmentPredictionDto?> PredictSegmentAsync(string customerId, CancellationToken ct = default)
    {
        var summary = await GetCustomerSummaryAsync(customerId, ct);
        if (summary is null)
            return null;
        // This is the lab's transparent rule-based demonstration, not a trained prediction model.
        var segment = summary.TotalSpend >= 1000 ? "High Value"
            : summary.TransactionCount == 1 ? "New"
            : summary.TotalSpend >= 200 ? "Regular" : "At Risk";
        return new(customerId, segment, segment == "High Value" ? 0.89 : 0.70,
            [summary.TotalSpend >= 1000 ? "high_total_spend" : "low_total_spend",
             summary.Categories.Length > 1 ? "multi_category" : "single_category",
             $"total_{summary.TotalSpend.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"]);
    }

    public async Task<RetailTransaction> AddTransactionAsync(RetailTransaction transaction, CancellationToken ct = default)
    {
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return transaction;
    }

    public async Task<bool> DeleteTransactionAsync(int id, CancellationToken ct = default) =>
        await db.Transactions.Where(t => t.Id == id).ExecuteDeleteAsync(ct) > 0;
}
