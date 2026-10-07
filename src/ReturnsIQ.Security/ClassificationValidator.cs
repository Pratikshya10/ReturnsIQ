using System.Text.Json;
using ReturnsIQ.Core;

namespace ReturnsIQ.Security;

public record ClassificationCheck(bool Ok, ReturnClassification? Value, string? Error);

public static class ClassificationValidator
{
    public static ClassificationCheck Validate(string? modelOutput)
    {
        if (string.IsNullOrWhiteSpace(modelOutput)) return Fail("Empty output.");

        try
        {
            using var doc = JsonDocument.Parse(StripFences(modelOutput));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Fail("Output is not a JSON object.");

            if (!TryStr(root, "category", out var category, out var err)) return Fail(err);
            if (!Taxonomy.Categories.Contains(category))
                return Fail($"category '{Short(category)}' is not an allowed value.");

            if (!TryStr(root, "sentiment", out var sentiment, out err)) return Fail(err);
            if (!Taxonomy.Sentiments.Contains(sentiment))
                return Fail($"sentiment '{Short(sentiment)}' is not an allowed value.");

            if (!TryStr(root, "root_cause", out var rootCause, out err)) return Fail(err);
            if (rootCause.Length > 200) return Fail("root_cause must be 200 characters or fewer.");

            if (!root.TryGetProperty("is_product_defect", out var d) ||
                d.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return Fail("is_product_defect must be true or false.");

            if (!root.TryGetProperty("confidence", out var c) || c.ValueKind != JsonValueKind.Number)
                return Fail("confidence must be a number.");
            var conf = c.GetDouble();
            if (conf is < 0 or > 1) return Fail("confidence must be between 0 and 1.");

            return new ClassificationCheck(true,
                new ReturnClassification(category, sentiment, rootCause, d.GetBoolean(), conf), null);
        }
        catch (JsonException)
        {
            return Fail("Output is not valid JSON.");
        }
    }

    private static bool TryStr(JsonElement root, string name, out string value, out string error)
    {
        value = ""; error = "";
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(el.GetString()))
        {
            error = $"{name} is missing or not a non-empty string.";
            return false;
        }
        value = el.GetString()!.Trim();
        return true;
    }

    private static string StripFences(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("```"))
        {
            var nl = s.IndexOf('\n');
            if (nl >= 0) s = s[(nl + 1)..];
            if (s.EndsWith("```")) s = s[..^3];
        }
        return s.Trim();
    }

    private static string Short(string s) => s.Length <= 30 ? s : s[..30] + "…";
    private static ClassificationCheck Fail(string error) => new(false, null, error);
}