using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace AgentHQDemo.Web.Services;

public sealed record ChatModel(string Id, string Name);
public sealed record ChatRequest(string Prompt, string Model, string SystemMessage);

public sealed class ChatApi(HttpClient http)
{
    public async Task<List<ChatModel>> GetModelsAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.GetAsync("api/chat/models", timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"模型接口返回 HTTP {(int)response.StatusCode}（{response.ReasonPhrase}）。");
        }
        var models = await response.Content.ReadFromJsonAsync<List<ChatModel>>(timeout.Token);
        return models?.Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .DistinctBy(model => model.Id).ToList() ?? [];
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest chat, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream")
        {
            Content = JsonContent.Create(chat)
        };
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.SetBrowserResponseStreamingEnabled(true);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"聊天接口返回 HTTP {(int)response.StatusCode}（{response.ReasonPhrase}）。");
        }
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
        {
            throw new InvalidDataException("聊天接口未返回 text/event-stream，请检查 API 地址与服务配置。");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        // StreamReader preserves UTF-8 characters and lines across arbitrary network chunks.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length != 0)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    var value = line.AsSpan(5);
                    if (value.StartsWith(" "))
                    {
                        value = value[1..];
                    }
                    data.Append(value).Append('\n');
                    if (data.Length > 1_048_576)
                    {
                        throw new InvalidDataException("单个流事件过大，已停止接收。");
                    }
                }
                continue;
            }

            if (data.Length == 0)
            {
                continue;
            }
            var payload = data.ToString().TrimEnd('\n');
            data.Clear();
            if (payload.Trim() == "[DONE]")
            {
                yield break;
            }

            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException(error.GetString() ?? "服务端返回未知错误。");
            }
            if (json.RootElement.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                yield return content.GetString()!;
            }
        }

        throw new EndOfStreamException("连接在完成标记前中断。已保留收到的内容，请重试。");
    }
}
