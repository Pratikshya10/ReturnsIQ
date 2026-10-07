namespace ReturnsIQ.Security;

public record SanitizedInput(
    string PromptText,
    string RedactedText,
    IReadOnlyDictionary<string, int> Redactions,
    GuardResult Guard);

public static class InputSanitizer
{
    public static SanitizedInput Prepare(string rawReason)
    {
        var redacted = PiiRedactor.Redact(rawReason);          // 1. remove PII first
        var guard = InjectionGuard.Inspect(redacted.Text);     // 2. flag attacks
        var prompt = UntrustedText.Wrap(redacted.Text);        // 3. wrap as untrusted data
        return new SanitizedInput(prompt, redacted.Text, redacted.Counts, guard);
    }
}