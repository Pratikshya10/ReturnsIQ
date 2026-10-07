using ReturnsIQ.Core;
using ReturnsIQ.Security;

namespace ReturnsIQ.Analysis;

public record ClassificationOutcome(
    ReturnClassification? Classification,
    bool NeedsHumanReview,
    bool ApiFailure,
    string? Error,
    SanitizedInput Input,
    int InputTokens,       // billed tokens only (cache hits excluded)
    int OutputTokens,
    double CostUsd,
    double LatencyMs,
    int Attempts,
    bool FromCache,
    string? RawOutput = null);

public sealed class ReasonClassifier(ILlmClient llm)
{
    private const int MaxTokens = 300;
    private const string RetryNotice =
        "\n\nYour previous reply was rejected because it was not a valid JSON object with the required " +
        "fields and allowed values. Reply with ONLY the JSON object.";

    public async Task<ClassificationOutcome> ClassifyAsync(string rawReason, CancellationToken ct = default)
    {
        var input = InputSanitizer.Prepare(rawReason);
        int inTok = 0, outTok = 0, attempts = 0;
        double cost = 0, latency = 0;
        bool allCached = true;
        string? lastRaw = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            attempts = attempt;
            var user = attempt == 1 ? input.PromptText : input.PromptText + RetryNotice;

            LlmResponse r;
            try
            {
                r = await llm.CompleteAsync(Prompts.SystemPrompt, user, MaxTokens, ct);
            }
            catch (LlmException ex)
            {
                return new ClassificationOutcome(null, false, true, ex.Message, input,
                    inTok, outTok, cost, latency, attempts, false, lastRaw);
            }

            lastRaw = r.Text;
            allCached &= r.FromCache;
            latency += r.LatencyMs;
            if (!r.FromCache)
            {
                inTok += r.Usage.InputTokens;
                outTok += r.Usage.OutputTokens;
                cost += CostCalculator.Estimate(r.Usage.InputTokens, r.Usage.OutputTokens);
            }

            var check = ClassificationValidator.Validate(r.Text);
            if (check.Ok)
                return new ClassificationOutcome(check.Value, false, false, null, input,
                    inTok, outTok, cost, latency, attempts, allCached, r.Text);
        }

        return new ClassificationOutcome(null, true, false, "Model output failed validation twice.", input,
            inTok, outTok, cost, latency, attempts, allCached, lastRaw);
    }
}