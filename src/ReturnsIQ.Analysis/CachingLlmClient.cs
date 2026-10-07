using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReturnsIQ.Analysis;

public sealed class CachingLlmClient(ILlmClient inner, string cacheDir, string cacheNamespace) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheDir);
        var key = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{cacheNamespace}\n{maxTokens}\n{system}\n{user}")));
        var path = Path.Combine(cacheDir, key + ".json");

        if (File.Exists(path))
        {
            var cached = JsonSerializer.Deserialize<LlmResponse>(await File.ReadAllTextAsync(path, ct));
            if (cached is not null) return cached with { FromCache = true, LatencyMs = 0 };
        }

        var fresh = await inner.CompleteAsync(system, user, maxTokens, ct);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(fresh), ct);
        return fresh;
    }
}