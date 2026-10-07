using System.Text.RegularExpressions;

namespace ReturnsIQ.Security;

public record GuardResult(bool IsSuspicious, IReadOnlyList<string> Hits);

public static class InjectionGuard
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    private const RegexOptions Opts =
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;

    private static Regex Rx(string pattern) => new(pattern, Opts, Timeout);

    private static readonly (string Name, Regex Rx)[] Rules =
    [
        ("override_instructions", Rx(@"\b(?:ignore|disregard|forget|override|bypass|skip)\b.{0,40}?\b(?:instructions?|rules?|above|previous|prior|prompt|validation|guidelines|task)\b")),
        ("new_instructions",      Rx(@"\b(?:new|updated|revised)\s+(?:system\s+)?(?:instructions?|message|prompt)\b")),
        ("system_marker",         Rx(@"(?:^|\s)system\s*:|###\s*(?:new\s+)?system")),
        ("role_switch",           Rx(@"\byou\s+are\s+now\s+(?:in\s+)?(?:an?\s+)?(?:admin|developer|debug|root|unrestricted|jailbroken)\b|\b(?:admin|developer|debug)\s+mode\b")),
        ("prompt_extraction",     Rx(@"\b(?:translate|repeat|print|reveal|show|output|display|leak)\b.{0,30}?\b(?:your|the)\s+(?:system\s+)?(?:instructions?|prompt|rules)\b")),
        ("data_exfiltration",     Rx(@"\b(?:reveal|list|show|send|leak)\b.{0,30}?\b(?:emails?|customer\s+data|api\s+keys?|secrets?|passwords?)\b")),
        ("delimiter_breakout",    Rx(@"</?\s*customer_text\s*>")),
        ("output_hijack",         Rx(@"\b(?:reply|respond|answer|output)\b.{0,25}?\b(?:only\s+with|with\s+json|exactly)\b|\b(?:category|confidence|is_product_defect|sentiment)\s*=")),
        ("tool_name_mention",     Rx(@"\b(?:flag_return_for_review|get_return_stats|list_returns|get_order)\b")),
        ("addresses_ai",          Rx(@"\b(?:to\s+the\s+(?:ai|assistant|model|llm)\b|assistant\s*,|dear\s+(?:ai|assistant)\b)")),
        ("authority_claim",       Rx(@"\bas\s+the\s+(?:store\s+)?(?:owner|admin(?:istrator)?|manager|developer)\b|\bi\s+authori[sz]e\s+you\b")),
    ];

    public static GuardResult Inspect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new GuardResult(false, Array.Empty<string>());

        var hits = new List<string>();
        foreach (var (name, rx) in Rules)
        {
            try { if (rx.IsMatch(text)) hits.Add(name); }
            catch (RegexMatchTimeoutException) { hits.Add("regex_timeout"); } // fail closed
        }
        return new GuardResult(hits.Count > 0, hits);
    }
}