using System.Text.Json;

namespace LIFT.Services;

public sealed record OllamaChatMessage(string Role, string Content);

public interface IOllamaClient
{
    Task<string> ChatAsync(string model, IReadOnlyList<OllamaChatMessage> messages,
        object? format, int maxTokens, CancellationToken cancellationToken);
}
