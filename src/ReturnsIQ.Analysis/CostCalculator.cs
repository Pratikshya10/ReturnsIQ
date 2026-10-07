namespace ReturnsIQ.Analysis;

public static class CostCalculator
{
    // USD per million tokens for Claude Haiku 4.5. Re-check Anthropic's pricing page.
    public const double InputPerMTok = 1.00;
    public const double OutputPerMTok = 5.00;

    public static double Estimate(int inputTokens, int outputTokens) =>
        inputTokens * InputPerMTok / 1_000_000.0 + outputTokens * OutputPerMTok / 1_000_000.0;
}