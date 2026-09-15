using System.ComponentModel.DataAnnotations;

namespace AgentHQDemo.Api.Services;

public sealed record ChatRequest(
    [Required, StringLength(16000, MinimumLength = 1)] string Prompt,
    [StringLength(200)] string? Model = null,
    [StringLength(8000)] string? SystemMessage = null);

public sealed record ChatModel(string Id, string Name);

public interface IChatService
{
    Task<IReadOnlyList<ChatModel>> ListModelsAsync(CancellationToken ct);
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken ct);
}
