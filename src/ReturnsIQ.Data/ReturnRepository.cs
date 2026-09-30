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
}