using System.Net.Http.Json;
using System.Text.Json;

namespace LIFT.Services;

public sealed class OllamaClient(HttpClient http) : IOllamaClient
{
    public async Task<string> ChatAsync(string model, IReadOnlyList<OllamaChatMessage> messages,
        object? format, int maxTokens, CancellationToken ct)
    {
        var request = new
        {
            model, messages = messages.Select((x, i) => new { role = x.Role,
                content = i == messages.Count - 1 && x.Role == "user" ? x.Content + "\n/no_think" : x.Content }).ToArray(),
            stream = false, think = false, format,
            options = new { temperature = 0.2, num_predict = maxTokens }
        };
        using var response = await http.PostAsJsonAsync("api/chat", request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 1_000_000) throw new InvalidDataException("AI response too large.");
        using var json = JsonDocument.Parse(body);
        var content = json.RootElement.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("AI response was empty.");
        content = System.Text.RegularExpressions.Regex.Replace(content,
            @"<think>[\s\S]*?</think>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("AI returned no final answer.");
        return content;
    }
}
