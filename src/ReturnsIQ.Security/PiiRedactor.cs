using System.Text.RegularExpressions;

namespace ReturnsIQ.Security;

public record RedactionResult(string Text, IReadOnlyDictionary<string, int> Counts);

public static class PiiRedactor
{
    // Timeouts protect against regex denial-of-service (ReDoS) from hostile input
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    private const RegexOptions Opts = RegexOptions.CultureInvariant;

    private static readonly Regex Email =
        new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Opts, Timeout);
    private static readonly Regex CardLike =
        new(@"\b(?:[0-9][ -]?){12,18}[0-9]\b", Opts, Timeout);
    private static readonly Regex PhoneLike =
        new(@"(?<!\w)\+?\(?[0-9][0-9\s\-().]{8,}[0-9](?!\w)", Opts, Timeout);

    public static RedactionResult Redact(string? input)
    {
        var counts = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(input)) return new RedactionResult("", counts);

        try
        {
            var text = Email.Replace(input, _ => Bump(counts, "EMAIL", "[EMAIL]"));
            // Cards first (13-19 digits), then phones (10-15 digits)
            text = CardLike.Replace(text, m =>
                DigitCount(m.Value) is >= 13 and <= 19 ? Bump(counts, "CARD", "[CARD]") : m.Value);
            text = PhoneLike.Replace(text, m =>
                DigitCount(m.Value) is >= 10 and <= 15 ? Bump(counts, "PHONE", "[PHONE]") : m.Value);
            return new RedactionResult(text, counts);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: never pass through text we couldn't scan
            return new RedactionResult("[REDACTION_FAILED]", new Dictionary<string, int> { ["TIMEOUT"] = 1 });
        }
    }

    private static string Bump(Dictionary<string, int> c, string key, string token)
    {
        c[key] = c.GetValueOrDefault(key) + 1;
        return token;
    }

    private static int DigitCount(string s) => s.Count(char.IsAsciiDigit);
}