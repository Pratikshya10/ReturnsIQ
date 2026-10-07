namespace ReturnsIQ.Analysis;

public record LlmUsage(int InputTokens, int OutputTokens);

public record LlmResponse(string Text, LlmUsage Usage, double LatencyMs, string Model, bool FromCache = false);

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct = default);
}

public class LlmException(string message) : Exception(message);