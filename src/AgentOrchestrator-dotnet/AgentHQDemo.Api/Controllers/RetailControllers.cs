using System.ComponentModel.DataAnnotations;
using AgentHQDemo.Core;
using Microsoft.AspNetCore.Mvc;

namespace AgentHQDemo.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(RetailAnalyticsService retail) : ControllerBase
{
    [HttpGet]
    public Task<List<RetailTransaction>> List([FromQuery] string? customerId, CancellationToken ct) =>
        retail.GetTransactionsAsync(customerId, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RetailTransaction>> Get(int id, CancellationToken ct) =>
        await retail.GetTransactionAsync(id, ct) is { } transaction ? Ok(transaction) : NotFound();

    [HttpPost]
    public async Task<ActionResult<RetailTransaction>> Add(NewTransaction request, CancellationToken ct)
    {
        if (await retail.GetSegmentAsync(request.SegmentId, ct) is null)
        {
            ModelState.AddModelError(nameof(request.SegmentId), "Segment does not exist.");
            return ValidationProblem(ModelState);
        }
        var transaction = await retail.AddTransactionAsync(
            new(0, request.CustomerId.Trim(), request.Category.Trim(), request.Amount, DateTime.UtcNow, request.SegmentId), ct);
        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, transaction);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await retail.DeleteTransactionAsync(id, ct) ? NoContent() : NotFound();
}

public sealed record NewTransaction(
    [Required, RegularExpression(@"C\d{3,12}")] string CustomerId,
    [Required, StringLength(100, MinimumLength = 1)] string Category,
    [Range(typeof(decimal), "0.01", "1000000000")] decimal Amount,
    [Range(1, int.MaxValue)] int SegmentId);

[ApiController]
[Route("api/segments")]
public sealed class SegmentsController(RetailAnalyticsService retail) : ControllerBase
{
    [HttpGet]
    public Task<List<CustomerSegment>> List(CancellationToken ct) => retail.GetSegmentsAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerSegment>> Get(int id, CancellationToken ct) =>
        await retail.GetSegmentAsync(id, ct) is { } segment ? Ok(segment) : NotFound();

    [HttpGet("predict/{customerId}")]
    public async Task<ActionResult<SegmentPredictionDto>> Predict(string customerId, CancellationToken ct) =>
        await retail.PredictSegmentAsync(customerId, ct) is { } prediction ? Ok(prediction) : NotFound();
}
