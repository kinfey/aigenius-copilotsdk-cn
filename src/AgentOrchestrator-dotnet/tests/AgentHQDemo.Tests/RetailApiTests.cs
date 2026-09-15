using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentHQDemo.Api.Services;
using AgentHQDemo.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentHQDemo.Tests;

public sealed class RetailApiFactory : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"agenthq-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Retail:DatabasePath", DatabasePath);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChatService>();
            services.AddSingleton<IChatService, FixtureChatService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
            File.Delete(DatabasePath + suffix);
    }

    private sealed class FixtureChatService : IChatService
    {
        public Task<IReadOnlyList<ChatModel>> ListModelsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ChatModel>>([new("fixture-model", "Test fixture")]);

        public async IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (request.Model == "unavailable")
                throw new InvalidOperationException("Model \"unavailable\" is not available.");
            yield return "你好";
            if (request.Prompt == "fail-after-delta")
                throw new InvalidOperationException("Fixture stream failure.");
            yield return "\n\"retail\"";
        }
    }
}

public sealed class RetailApiTests(RetailApiFactory factory) : IClassFixture<RetailApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/chat/health")]
    [InlineData("/api/chat/models")]
    [InlineData("/api/transactions")]
    [InlineData("/api/transactions/7")]
    [InlineData("/api/segments")]
    [InlineData("/api/segments/1")]
    [InlineData("/api/segments/predict/C003")]
    public async Task DocumentedEndpointsAreAvailable(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SeedHasTenTransactionsAndFourSegments()
    {
        var transactions = await _client.GetFromJsonAsync<RetailTransaction[]>("/api/transactions");
        var segments = await _client.GetFromJsonAsync<CustomerSegment[]>("/api/segments");
        Assert.Equal(10, transactions!.Length);
        Assert.Equal(4, segments!.Length);
        Assert.Equal("At Risk", segments.MinBy(s => s.RetentionRate)!.Name);
        Assert.Equal(45, segments.Min(s => s.RetentionRate));
    }

    [Fact]
    public async Task CustomerC003HasExpectedTotalAndCategories()
    {
        var rows = await _client.GetFromJsonAsync<RetailTransaction[]>("/api/transactions?customerId=C003");
        Assert.Equal(2, rows!.Length);
        Assert.Equal(1700m, rows.Sum(t => t.Amount));
        Assert.Equal(["Electronics", "Fashion"], rows.Select(t => t.Category).Order());
    }

    [Fact]
    public async Task PredictionMatchesLabCheckpoint()
    {
        var prediction = await _client.GetFromJsonAsync<SegmentPredictionDto>("/api/segments/predict/C003");
        Assert.Equal("High Value", prediction!.PredictedSegment);
        Assert.Equal(0.89, prediction.Confidence);
        Assert.Equal(["high_total_spend", "multi_category", "total_1700"], prediction.TopFeatures);
    }

    [Theory]
    [InlineData("/api/transactions/999")]
    [InlineData("/api/segments/999")]
    [InlineData("/api/segments/predict/C999")]
    public async Task MissingDataReturnsNotFound(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("C008", "Home", 0, 1)]
    [InlineData("C008", "Home", -1, 1)]
    [InlineData("", "Home", 10, 1)]
    [InlineData("invalid", "Home", 10, 1)]
    [InlineData("C008", "", 10, 1)]
    [InlineData("C008", " ", 10, 1)]
    [InlineData("C008", "Home", 10, 999)]
    public async Task InvalidTransactionsReturnBadRequest(string customer, string category, decimal amount, int segment)
    {
        using var response = await _client.PostAsJsonAsync("/api/transactions",
            new { customerId = customer, category, amount, segmentId = segment });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReadDeleteRoundTrip()
    {
        using var created = await _client.PostAsJsonAsync("/api/transactions",
            new { customerId = "C008", category = "Home", amount = 99m, segmentId = 4 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location;
        Assert.NotNull(location);
        var saved = await _client.GetFromJsonAsync<RetailTransaction>(location);
        Assert.Equal(99m, saved!.Amount);
        using var deleted = await _client.DeleteAsync(location);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task UnknownDeleteReturnsNotFound()
    {
        using var response = await _client.DeleteAsync("/api/transactions/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StreamHasJsonFramesBlankLinesAndDone()
    {
        using var response = await _client.PostAsJsonAsync("/api/chat/stream", new { prompt = "hello" });
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
        var frames = (await response.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, frames.Length);
        Assert.All(frames, frame => Assert.StartsWith("data: ", frame));
        Assert.Equal("你好", JsonDocument.Parse(frames[0][6..]).RootElement.GetProperty("content").GetString());
        Assert.Equal("\n\"retail\"", JsonDocument.Parse(frames[1][6..]).RootElement.GetProperty("content").GetString());
        Assert.Equal("data: [DONE]", frames[2]);
    }

    [Theory]
    [InlineData("hello", "unavailable")]
    [InlineData("fail-after-delta", null)]
    public async Task StreamFailuresAreErrorFramesNotFalseSuccess(string prompt, string? model)
    {
        using var response = await _client.PostAsJsonAsync("/api/chat/stream", new { prompt, model });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"error\":", body);
        Assert.DoesNotContain("[DONE]", body);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyPromptsAreRejectedBeforeStreaming(string prompt)
    {
        using var response = await _client.PostAsJsonAsync("/api/chat/stream", new { prompt });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonStreamingChatConcatenatesDeltas()
    {
        using var response = await _client.PostAsJsonAsync("/api/chat", new { prompt = "hello" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("你好\n\"retail\"", json.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SqliteReadOnlyConnectionRejectsWrites()
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = factory.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Transactions WHERE Id = 1";
        var exception = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(8, exception.SqliteErrorCode);
    }

    [Fact]
    public async Task InitializationDoesNotDuplicateSeedData()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
        await db.InitializeAsync();
        Assert.Equal(10, await db.Transactions.CountAsync());
        Assert.Equal(4, await db.Segments.CountAsync());
    }
}
