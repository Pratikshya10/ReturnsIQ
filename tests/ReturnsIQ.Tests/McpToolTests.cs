using ModelContextProtocol;
using ReturnsIQ.Data;
using ReturnsIQ.McpServer;
using System.Net.Mail;
using System.Text.Json;
using Xunit;

public class McpToolTests
{
    private static ReturnTools NewTools()
    {
        var db = new Database(Path.Combine(Path.GetTempPath(), $"mcp-{Guid.NewGuid()}.db"));
        db.Init();
        Seeder.Seed(db, Path.Combine(RepoPaths.DataDir(), "seed.json"));
        return new ReturnTools(new ReturnRepository(db));
    }

    [Fact]
    public void GetOrder_masks_email()
    {
        var json = NewTools().GetOrder(1001);
        Assert.Contains("***@example.com", json);
        Assert.DoesNotContain("fatima", json);
    }

    [Fact]
    public void Stats_detects_the_SKU123_spike()
    {
        var json = NewTools().GetReturnStats("SKU-123", "22-09-2026", "29-09-2026");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(22, doc.RootElement.GetProperty("period").GetProperty("total").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("previousPeriod").GetProperty("total").GetInt32());
    }

    [Fact]
    public void Invalid_sku_is_rejected()
        => Assert.Throws<McpException>(() => NewTools().GetReturnStats("SKU-1; DROP TABLE", "22-09-2026", "29-09-2026"));

    [Fact]
    public void Oversized_date_range_is_rejected()
        => Assert.Throws<McpException>(() => NewTools().ListReturns("01-01-2026", "29-09-2026"));

    [Fact]
    public void Flag_only_queues_and_rejects_duplicates()
    {
        var tools = NewTools();
        var json = tools.FlagReturnForReview(179, "Cluster of cracked mugs");
        Assert.Contains("PendingApproval", json);
        Assert.Throws<McpException>(() => tools.FlagReturnForReview(179, "again"));
    }
}