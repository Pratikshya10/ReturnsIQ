using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ReturnsIQ.Analysis;

public sealed class ClaudeLlmClient : ILlmClient
{
    public const string DefaultModel = "claude-haiku-4-5-20251001";
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly string _model;

    public ClaudeLlmClient(HttpClient http, string apiKey, string model = DefaultModel)
    {
        _http = http;
        _model = model;
        _http.BaseAddress ??= new Uri("https://api.anthropic.com/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<LlmResponse> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = maxTokens,
            temperature = 0,
            system,
            messages = new[] { new { role = "user", content = user } }
        });

        for (var attempt = 1; ; attempt++)
        {
            TimeSpan delay;
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                using var res = await _http.SendAsync(req, ct);
                var payload = await res.Content.ReadAsStringAsync(ct);

                if (res.IsSuccessStatusCode) return Parse(payload, sw.Elapsed.TotalMilliseconds);

                if (!IsTransient(res.StatusCode) || attempt >= MaxAttempts)
                    throw new LlmException($"Claude API error {(int)res.StatusCode}: {Trunc(payload, 300)}");

                delay = CapDelay(res.Headers.RetryAfter?.Delta) ?? Backoff(attempt);
            }
            catch (Exception ex) when (ex is HttpRequestException ||
                                       (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                if (attempt >= MaxAttempts) throw new LlmException($"Network or timeout error: {ex.Message}");
                delay = Backoff(attempt);
            }
            await Task.Delay(delay, ct);
        }
    }

    private LlmResponse Parse(string payload, double ms)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            foreach (var block in root.GetProperty("content").EnumerateArray())
                if (block.GetProperty("type").GetString() == "text")
                    sb.Append(block.GetProperty("text").GetString());
            var usage = root.GetProperty("usage");
            return new LlmResponse(sb.ToString(),
                new LlmUsage(usage.GetProperty("input_tokens").GetInt32(),
                             usage.GetProperty("output_tokens").GetInt32()),
                ms, _model);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new LlmException("Unexpected response shape from Claude API.");
        }
    }

    private static bool IsTransient(HttpStatusCode code) =>
        (int)code is 408 or 429 or 500 or 502 or 503 or 504 or 529;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));

    private static TimeSpan? CapDelay(TimeSpan? d) =>
        d is null ? null : TimeSpan.FromSeconds(Math.Min(d.Value.TotalSeconds, 20));

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}