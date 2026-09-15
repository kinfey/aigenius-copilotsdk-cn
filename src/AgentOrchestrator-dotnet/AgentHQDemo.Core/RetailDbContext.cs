using Microsoft.EntityFrameworkCore;

namespace AgentHQDemo.Core;

public sealed class RetailDbContext(DbContextOptions<RetailDbContext> options) : DbContext(options)
{
    public DbSet<RetailTransaction> Transactions => Set<RetailTransaction>();
    public DbSet<CustomerSegment> Segments => Set<CustomerSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RetailTransaction>().HasIndex(t => t.CustomerId);
        modelBuilder.Entity<RetailTransaction>().HasOne<CustomerSegment>()
            .WithMany().HasForeignKey(t => t.SegmentId);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Database.EnsureCreatedAsync(cancellationToken);
        if (await Segments.AnyAsync(cancellationToken))
            return;

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        Segments.AddRange(
            new CustomerSegment(1, "High Value", 92, "Frequent, high-spending customers"),
            new CustomerSegment(2, "Regular", 78, "Returning customers"),
            new CustomerSegment(3, "At Risk", 45, "Customers who may not return"),
            new CustomerSegment(4, "New", 65, "Recently acquired customers"));
        await SaveChangesAsync(cancellationToken);
        Transactions.AddRange(
            Seed(1, "C001", "Electronics", 650, 1),
            Seed(2, "C002", "Grocery", 120, 2),
            Seed(3, "C003", "Electronics", 1200, 1),
            Seed(4, "C004", "Fashion", 85, 3),
            Seed(5, "C005", "Home", 250, 4),
            Seed(6, "C001", "Home", 450, 1),
            Seed(7, "C003", "Fashion", 500, 1),
            Seed(8, "C002", "Fashion", 180, 2),
            Seed(9, "C006", "Grocery", 60, 4),
            Seed(10, "C004", "Grocery", 40, 3));
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static RetailTransaction Seed(int id, string customer, string category, decimal amount, int segment) =>
        new(id, customer, category, amount, new DateTime(2026, 1, id, 12, 0, 0, DateTimeKind.Utc), segment);
}

public sealed record CustomerSegment(int Id, string Name, int RetentionRate, string Description);
public sealed record RetailTransaction(
    int Id, string CustomerId, string Category, decimal Amount, DateTime Date, int SegmentId);
public sealed record CustomerSummaryDto(
    string CustomerId, decimal TotalSpend, decimal AverageSpend, int TransactionCount, string[] Categories);
public sealed record SegmentPredictionDto(
    string CustomerId, string PredictedSegment, double Confidence, string[] TopFeatures);
