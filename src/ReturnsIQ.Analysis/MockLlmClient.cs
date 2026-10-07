using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReturnsIQ.Analysis;

public sealed class MockLlmClient : ILlmClient
{
    private static Regex R(string p) =>
        new(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    private static readonly (string Category, string Sentiment, bool Defect, string Cause, Regex Rx)[] Rules =
    [
        ("DEFECTIVE_PRODUCT", "negative", true,  "Product fault",           R(@"stopped working|won't turn on|no sound|leak|peeling|defective|buzz|won't pair|\bdead\b|tear along|split open|zipper|stitching|doesn't hold|dies in|cuts out|never seals")),
        ("WRONG_ITEM",        "negative", false, "Wrong item shipped",      R(@"wrong|not what i ordered|someone else|but received|got something else")),
        ("SIZE_OR_FIT",       "neutral",  false, "Sizing mismatch",         R(@"too small|too tight|too large|runs very large|fits like|swimming in|sleeves are|narrower|size")),
        ("DAMAGED_IN_TRANSIT","negative", false, "Item damaged in transit", R(@"broken|cracked|crushed|smashed|shattered|damaged|dented|big dent|chipped|snapped|pieces|no padding|bubble wrap|cushion|dropped|ripped|\bbent\b")),
        ("LATE_DELIVERY",     "negative", false, "Delivery delay",          R(@"\blate\b|took forever|stuck for|delivery took|after the date|took 12")),
        ("NOT_AS_DESCRIBED",  "negative", false, "Listing mismatch",        R(@"described|description|photos|pictures|listing|advertised|misleading|flimsy|waterproof")),
        ("CHANGED_MIND",      "neutral",  false, "Customer preference",     R(@"changed my mind|don't need|by accident|duplicate|cheaper|already has one|decided i don't")),
    ];

    public Task<LlmResponse> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct = default)
    {
        var rule = Rules.FirstOrDefault(r => r.Rx.IsMatch(user));
        var (cat, sent, defect, cause) = rule.Rx is null
            ? ("OTHER", "neutral", false, "Unclear reason")
            : (rule.Category, rule.Sentiment, rule.Defect, rule.Cause);

        var json = JsonSerializer.Serialize(new
        {
            category = cat,
            sentiment = sent,
            root_cause = cause,
            is_product_defect = defect,
            confidence = 0.8
        });
        return Task.FromResult(new LlmResponse(json, new LlmUsage(0, 0), 0, "mock"));
    }
}