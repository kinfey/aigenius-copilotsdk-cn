using System.Text;
using System.Text.Json;
using AgentHQDemo.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace AgentHQDemo.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(IChatService chat, ILogger<ChatController> logger) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", service = "Agent HQ API", copilot = "Check /api/chat/models for live SDK connectivity." });

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken ct)
    {
        try
        {
            return Ok(await chat.ListModelsAsync(ct));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not discover Copilot models");
            return Problem(statusCode: 503, title: "Copilot model discovery failed", detail: exception.Message);
        }
    }

    [HttpPost("stream")]
    public async Task StreamChat(ChatRequest request, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try
        {
            await foreach (var content in chat.ChatStreamAsync(request, ct))
                await WriteFrameAsync(JsonSerializer.Serialize(new { content }), ct);
            await WriteFrameAsync("[DONE]", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("Chat stream disconnected");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Chat stream failed");
            if (!ct.IsCancellationRequested)
                await WriteFrameAsync(JsonSerializer.Serialize(new { error = exception.Message }), ct);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Chat(ChatRequest request, CancellationToken ct)
    {
        try
        {
            var content = new StringBuilder();
            await foreach (var delta in chat.ChatStreamAsync(request, ct))
                content.Append(delta);
            return Ok(new { content = content.ToString() });
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogError(exception, "Chat failed");
            return Problem(statusCode: 502, title: "Copilot chat failed", detail: exception.Message);
        }
    }

    private async Task WriteFrameAsync(string data, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
