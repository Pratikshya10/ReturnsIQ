using System.Text.Json;
using ReturnsIQ.Core;

namespace ReturnsIQ.Data;

public static class Seeder
{
    public static void Seed(Database db, string jsonPath)
    {
        var seed = JsonSerializer.Deserialize<SeedData>(File.ReadAllText(jsonPath))!;
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        Exec(conn, tx, "DELETE FROM Returns; DELETE FROM OrderItems; DELETE FROM Orders; DELETE FROM Products;");

        foreach (var p in seed.Products)
            Exec(conn, tx, "INSERT INTO Products VALUES ($a,$b,$c,$d)",
                ("$a", p.Sku), ("$b", p.Name), ("$c", p.Category), ("$d", p.Price));

        foreach (var o in seed.Orders)
            Exec(conn, tx, "INSERT INTO Orders VALUES ($a,$b,$c)",
                ("$a", o.OrderId), ("$b", o.CustomerEmail), ("$c", o.OrderedAt.ToString("o")));

        foreach (var i in seed.OrderItems)
            Exec(conn, tx, "INSERT INTO OrderItems VALUES ($a,$b,$c)",
                ("$a", i.OrderId), ("$b", i.Sku), ("$c", i.Quantity));

        foreach (var r in seed.Returns)
            Exec(conn, tx, "INSERT INTO Returns VALUES ($a,$b,$c,$d,$e,$f)",
                ("$a", r.ReturnId), ("$b", r.OrderId), ("$c", r.Sku),
                ("$d", r.Reason), ("$e", r.CreatedAt.ToString("o")), ("$f", r.Status));

        tx.Commit();
    }

    private static void Exec(Microsoft.Data.Sqlite.SqliteConnection c,
        Microsoft.Data.Sqlite.SqliteTransaction tx, string sql,
        params (string, object)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        cmd.ExecuteNonQuery();
    }
}