using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ReturnsIQ.Core;
using ReturnsIQ.Data;
using ReturnsIQ.Security;
using Xunit;

public class DatasetSecurityTests
{
    public record EvalExpected(
        [property: JsonPropertyName("injection_attempt")] bool InjectionAttempt,
        [property: JsonPropertyName("contains_pii")] bool ContainsPii);

    public record EvalItem(int ReturnId, string Reason, EvalExpected Expected);

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static List<ReturnRequest> LoadReturns() =>
        JsonSerializer.Deserialize<SeedData>(
            File.ReadAllText(Path.Combine(RepoPaths.DataDir(), "seed.json")), Opts)!.Returns;

    [Fact]
    public void Full_dataset_counts_match()
    {
        var all = LoadReturns().Select(r => InputSanitizer.Prepare(r.Reason)).ToList();
        Assert.Equal(200, all.Count);
        Assert.Equal(12, all.Count(s => s.Guard.IsSuspicious));
        Assert.Equal(22, all.Count(s => s.Redactions.Count > 0));
    }

    [Fact]
    public void No_pii_survives_redaction()
    {
        foreach (var r in LoadReturns())
        {
            var text = InputSanitizer.Prepare(r.Reason).RedactedText;
            Assert.DoesNotContain("@", text);
            Assert.False(Regex.IsMatch(text, @"[0-9][0-9 \-]{8,}[0-9]"), $"Return {r.ReturnId} still has digits: {text}");
        }
    }

    [Fact]
    public void Every_prompt_has_exactly_one_closing_tag()
    {
        foreach (var r in LoadReturns())
        {
            var prompt = InputSanitizer.Prepare(r.Reason).PromptText;
            Assert.Equal(1, prompt.Split("</customer_text>").Length - 1);
        }
    }

    [Fact]
    public void Eval_labels_for_injection_and_pii_match()
    {
        var items = JsonSerializer.Deserialize<List<EvalItem>>(
            File.ReadAllText(Path.Combine(RepoPaths.DataDir(), "eval_labels.json")), Opts)!;
        Assert.Equal(50, items.Count);

        var problems = new List<string>();
        foreach (var e in items)
        {
            var p = InputSanitizer.Prepare(e.Reason);
            if (p.Guard.IsSuspicious != e.Expected.InjectionAttempt)
                problems.Add($"#{e.ReturnId} injection expected {e.Expected.InjectionAttempt}: {e.Reason}");
            if ((p.Redactions.Count > 0) != e.Expected.ContainsPii)
                problems.Add($"#{e.ReturnId} pii expected {e.Expected.ContainsPii}: {e.Reason}");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}