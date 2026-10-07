using System.Text.Json;
using ReturnsIQ.Analysis;
using ReturnsIQ.Core;
using ReturnsIQ.Data;
using ReturnsIQ.Security;

var dataDir = RepoPaths.DataDir();
var dbPath = Path.Combine(dataDir, "returnsiq.db");

switch (args.FirstOrDefault()?.ToLowerInvariant())
{
    case "seed": Seed(); break;
    case "security-demo": SecurityDemo(); break;
    case "classify": await ClassifyAsync(args.Skip(1).ToArray()); break;
    default:
        Console.WriteLine("Usage: dotnet run --project src/ReturnsIQ.Agent -- <command>");
        Console.WriteLine("  seed                      create a fresh database from data/seed.json");
        Console.WriteLine("  security-demo             show PII redaction and injection flags");
        Console.WriteLine("  classify [--live] [--limit N] [--redo] [--max-cost USD] [--model NAME]");
        break;
}
return;

void Seed()
{
    if (File.Exists(dbPath)) File.Delete(dbPath);   // fails if Inspector or DB Browser has it open
    var db = new Database(dbPath);
    db.Init();
    Seeder.Seed(db, Path.Combine(dataDir, "seed.json"));
    Console.WriteLine($"Seeded fresh database: {dbPath}");
}

void SecurityDemo()
{
    var seed = JsonSerializer.Deserialize<SeedData>(File.ReadAllText(Path.Combine(dataDir, "seed.json")))!;
    int redacted = 0, flagged = 0;
    foreach (var r in seed.Returns)
    {
        var p = InputSanitizer.Prepare(r.Reason);
        if (p.Redactions.Count > 0) { redacted++; if (redacted <= 2) Console.WriteLine($"[PII]  #{r.ReturnId}: {p.RedactedText}"); }
        if (p.Guard.IsSuspicious) { flagged++; if (flagged <= 3) Console.WriteLine($"[INJ]  #{r.ReturnId}: {string.Join(",", p.Guard.Hits)}"); }
    }
    Console.WriteLine($"Redacted {redacted}/200, flagged {flagged}/200");
}

async Task ClassifyAsync(string[] a)
{
    var live = a.Contains("--live");
    var redo = a.Contains("--redo");
    var limit = int.TryParse(Opt(a, "--limit"), out var l) ? l : 20;
    var maxCost = double.TryParse(Opt(a, "--max-cost"), System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var mc) ? mc : 0.50;
    var model = Opt(a, "--model") ?? ClaudeLlmClient.DefaultModel;

    ILlmClient llm;
    string modelLabel;
    if (live)
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("ANTHROPIC_API_KEY is not set. Set it and restart your terminal.");
            return;
        }
        llm = new CachingLlmClient(new ClaudeLlmClient(new HttpClient(), key, model),
            Path.Combine(dataDir, "cache"), model);
        modelLabel = model;
    }
    else
    {
        llm = new MockLlmClient();
        modelLabel = "mock";
    }

    var db = new Database(dbPath);
    db.Init();   // makes sure newer tables exist
    var repo = new ReturnRepository(db);
    var classifier = new ReasonClassifier(llm);
    var todo = repo.ListForClassification(limit, onlyUnanalyzed: !redo);

    Console.WriteLine($"Mode: {(live ? "LIVE" : "MOCK")} | model: {modelLabel} | returns to process: {todo.Count} | max cost: ${maxCost:F2}");

    int processed = 0, classified = 0, review = 0, apiFail = 0, consecutiveFail = 0, injected = 0, withPii = 0, cacheHits = 0, liveCalls = 0;
    long tokIn = 0, tokOut = 0;
    double cost = 0, latency = 0;

    foreach (var r in todo)
    {
        if (cost >= maxCost) { Console.WriteLine($"Stopped: reached max cost ${maxCost:F2}."); break; }

        var o = await classifier.ClassifyAsync(r.Reason);
        processed++;

        if (o.ApiFailure)
        {
            apiFail++; consecutiveFail++;
            Console.WriteLine($"#{r.ReturnId,4} API FAILURE: {o.Error}");
            if (consecutiveFail >= 3) { Console.WriteLine("Stopping after 3 consecutive API failures."); break; }
            continue;
        }
        consecutiveFail = 0;

        if (o.Input.Guard.IsSuspicious) injected++;
        if (o.Input.Redactions.Count > 0) withPii++;
        tokIn += o.InputTokens; tokOut += o.OutputTokens; cost += o.CostUsd;
        if (o.FromCache) cacheHits++; else { liveCalls++; latency += o.LatencyMs; }

        var tags = (o.Input.Guard.IsSuspicious ? " [INJ]" : "") + (o.Input.Redactions.Count > 0 ? " [PII]" : "");
        var timing = o.FromCache ? "cached" : $"{o.LatencyMs:F0}ms";

        if (o.Classification is { } c)
        {
            repo.SaveAnalysis(new ReturnAnalysis(r.ReturnId, c.Category, c.Sentiment, c.RootCause,
                c.IsProductDefect, c.Confidence, modelLabel, DateTime.UtcNow));
            classified++;
            Console.WriteLine($"#{r.ReturnId,4} {c.Category,-19} {c.Sentiment,-8} defect={c.IsProductDefect,-5} conf={c.Confidence:F2} ${o.CostUsd:F4} {timing}{tags}");
        }
        else
        {
            review++;
            var raw = o.RawOutput is null ? "" : (o.RawOutput.Length > 100 ? o.RawOutput[..100] + "…" : o.RawOutput);
            Console.WriteLine($"#{r.ReturnId,4} NEEDS HUMAN REVIEW (raw: {raw}){tags}");
        }

        repo.SaveSafety(r.ReturnId, o.Input.Guard.IsSuspicious, string.Join(",", o.Input.Guard.Hits),
            o.Input.Redactions.Values.Sum(), o.NeedsHumanReview);
    }

    Console.WriteLine();
    Console.WriteLine($"Processed {processed} | classified {classified} | needs review {review} | API failures {apiFail}");
    Console.WriteLine($"Injection suspected: {injected} | Rows with PII redacted: {withPii}");
    Console.WriteLine($"Billed tokens in/out: {tokIn:N0} / {tokOut:N0} | Cost: ${cost:F4} | Cache hits: {cacheHits} | " +
                      $"Avg live latency: {(liveCalls == 0 ? "n/a" : $"{latency / liveCalls:F0}ms")}");
    if (live && liveCalls > 0)
        Console.WriteLine($"Projected cost per 1,000 returns: ${cost / liveCalls * 1000:F2}");
}

string? Opt(string[] a, string name)
{
    var i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}