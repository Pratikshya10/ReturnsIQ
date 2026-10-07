using ReturnsIQ.Core;

namespace ReturnsIQ.Data;

public class ReturnRepository(Database db)
{
    public Order? GetOrder(int orderId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT OrderId, CustomerEmail, OrderedAt FROM Orders WHERE OrderId=$id";
        cmd.Parameters.AddWithValue("$id", orderId);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new Order(r.GetInt32(0), r.GetString(1), DateTime.Parse(r.GetString(2)))
            : null;
    }

    public List<ReturnRequest> ListReturns(string? sku, DateTime from, DateTime to, int limit = 50)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ReturnId, OrderId, Sku, Reason, CreatedAt, Status FROM Returns
            WHERE ($sku IS NULL OR Sku=$sku) AND CreatedAt >= $from AND CreatedAt <= $to
            ORDER BY CreatedAt DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$sku", (object?)sku ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$from", from.ToString("o"));
        cmd.Parameters.AddWithValue("$to", to.ToString("o"));
        cmd.Parameters.AddWithValue("$limit", Math.Min(limit, 100)); // hard cap
        var list = new List<ReturnRequest>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ReturnRequest(r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.GetString(3), DateTime.Parse(r.GetString(4)), r.GetString(5)));
        return list;
    }

    public Dictionary<string, int> GetStats(string sku, DateTime from, DateTime to)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(a.Category,'Unclassified'), COUNT(*)
            FROM Returns r LEFT JOIN ReturnAnalysis a ON a.ReturnId = r.ReturnId
            WHERE r.Sku=$sku AND r.CreatedAt >= $from AND r.CreatedAt <= $to
            GROUP BY 1
            """;
        cmd.Parameters.AddWithValue("$sku", sku);
        cmd.Parameters.AddWithValue("$from", from.ToString("o"));
        cmd.Parameters.AddWithValue("$to", to.ToString("o"));
        var d = new Dictionary<string, int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) d[r.GetString(0)] = r.GetInt32(1);
        return d;
    }

    public List<OrderItem> GetOrderItems(int orderId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT OrderId, Sku, Quantity FROM OrderItems WHERE OrderId=$id";
        cmd.Parameters.AddWithValue("$id", orderId);
        var list = new List<OrderItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new OrderItem(r.GetInt32(0), r.GetString(1), r.GetInt32(2)));
        return list;
    }

    public ReturnRequest? GetReturn(int returnId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ReturnId, OrderId, Sku, Reason, CreatedAt, Status FROM Returns WHERE ReturnId=$id";
        cmd.Parameters.AddWithValue("$id", returnId);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new ReturnRequest(r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.GetString(3), DateTime.Parse(r.GetString(4)), r.GetString(5))
            : null;
    }

    public bool HasPendingFlag(int returnId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ReviewFlags WHERE ReturnId=$id AND Status='PendingApproval'";
        cmd.Parameters.AddWithValue("$id", returnId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public int CountPendingFlags()
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ReviewFlags WHERE Status='PendingApproval'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int AddReviewFlag(int returnId, string reason)
    {
        using var conn = db.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO ReviewFlags (ReturnId, Reason, Status, CreatedAt) VALUES ($r,$reason,'PendingApproval',$at)";
            cmd.Parameters.AddWithValue("$r", returnId);
            cmd.Parameters.AddWithValue("$reason", reason);
            cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        using var idCmd = conn.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(idCmd.ExecuteScalar());
    }

    public List<ReturnRequest> ListForClassification(int limit, bool onlyUnanalyzed)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.ReturnId, r.OrderId, r.Sku, r.Reason, r.CreatedAt, r.Status
            FROM Returns r
            WHERE $all = 1 OR (NOT EXISTS (SELECT 1 FROM ReturnAnalysis a WHERE a.ReturnId = r.ReturnId)
                           AND NOT EXISTS (SELECT 1 FROM ReturnSafety s WHERE s.ReturnId = r.ReturnId))
            ORDER BY r.ReturnId LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$all", onlyUnanalyzed ? 0 : 1);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        var list = new List<ReturnRequest>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ReturnRequest(r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.GetString(3), DateTime.Parse(r.GetString(4)), r.GetString(5)));
        return list;
    }

    public void SaveAnalysis(ReturnAnalysis a)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO ReturnAnalysis
            (ReturnId, Category, Sentiment, RootCause, IsProductDefect, Confidence, Model, AnalyzedAt)
            VALUES ($id,$cat,$sent,$root,$def,$conf,$model,$at)
            """;
        cmd.Parameters.AddWithValue("$id", a.ReturnId);
        cmd.Parameters.AddWithValue("$cat", a.Category);
        cmd.Parameters.AddWithValue("$sent", a.Sentiment);
        cmd.Parameters.AddWithValue("$root", a.RootCause);
        cmd.Parameters.AddWithValue("$def", a.IsProductDefect ? 1 : 0);
        cmd.Parameters.AddWithValue("$conf", a.Confidence);
        cmd.Parameters.AddWithValue("$model", a.Model);
        cmd.Parameters.AddWithValue("$at", a.AnalyzedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void SaveSafety(int returnId, bool injectionSuspected, string guardHits, int redactionCount, bool needsHumanReview)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
               INSERT OR REPLACE INTO ReturnSafety
               (ReturnId, InjectionSuspected, GuardHits, RedactionCount, NeedsHumanReview, CheckedAt)
               VALUES ($id,$inj,$hits,$red,$rev,$at)
               """;
        cmd.Parameters.AddWithValue("$id", returnId);
        cmd.Parameters.AddWithValue("$inj", injectionSuspected ? 1 : 0);
        cmd.Parameters.AddWithValue("$hits", guardHits);
        cmd.Parameters.AddWithValue("$red", redactionCount);
        cmd.Parameters.AddWithValue("$rev", needsHumanReview ? 1 : 0);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }


}