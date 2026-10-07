using System;
using ReturnsIQ.Data;
using ReturnsIQ.Core;
using ReturnsIQ.Security;

// find the repo's data folder by walking up from the bin directory
var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "seed.json")))
    dir = dir.Parent;
var dataDir = Path.Combine(dir!.FullName, "data");

var db = new Database(Path.Combine(dataDir, "returnsiq.db"));
db.Init();
Seeder.Seed(db, Path.Combine(dataDir, "seed.json"));
var repo = new ReturnRepository(db);

var from = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);
var to = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

Console.WriteLine($"Database: {db.ConnectionString}");
Console.WriteLine($"SKU-123 returns {from:yyyy-MM-dd} to {to:yyyy-MM-dd}: {repo.ListReturns("SKU-123", from, to).Count}");
foreach (var kv in repo.GetStats("SKU-123", from, to))
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
Console.WriteLine($"Order 1001: {repo.GetOrder(1001)?.CustomerEmail}");


//------- Phase 3 demo
var seed = System.Text.Json.JsonSerializer.Deserialize<SeedData>(
    File.ReadAllText(Path.Combine(dataDir, "seed.json")))!;
int redacted = 0, flagged = 0;
foreach (var r in seed.Returns)
{
    var p = InputSanitizer.Prepare(r.Reason);
    if (p.Redactions.Count > 0) { redacted++; if (redacted <= 2) Console.WriteLine($"[PII]  #{r.ReturnId}: {p.RedactedText}"); }
    if (p.Guard.IsSuspicious) { flagged++; if (flagged <= 3) Console.WriteLine($"[INJ]  #{r.ReturnId}: {string.Join(",", p.Guard.Hits)}"); }
}
Console.WriteLine($"Redacted {redacted}/200, flagged {flagged}/200");