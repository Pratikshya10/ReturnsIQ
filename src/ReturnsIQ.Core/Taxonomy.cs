namespace ReturnsIQ.Core;

public static class Taxonomy
{
    public static readonly IReadOnlySet<string> Categories = new HashSet<string>
    {
        "DAMAGED_IN_TRANSIT", "DEFECTIVE_PRODUCT", "WRONG_ITEM", "SIZE_OR_FIT",
        "NOT_AS_DESCRIBED", "LATE_DELIVERY", "CHANGED_MIND", "OTHER"
    };

    public static readonly IReadOnlySet<string> Sentiments = new HashSet<string>
    {
        "positive", "neutral", "negative"
    };
}

public record ReturnClassification(
    string Category, string Sentiment, string RootCause,
    bool IsProductDefect, double Confidence);