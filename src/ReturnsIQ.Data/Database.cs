using Microsoft.Data.Sqlite;

namespace ReturnsIQ.Data;

public class Database(string dbPath)
{
    public string ConnectionString { get; } = $"Data Source={dbPath}";

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public void Init()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Products (
                Sku TEXT PRIMARY KEY, Name TEXT NOT NULL,
                Category TEXT NOT NULL, Price REAL NOT NULL);

            CREATE TABLE IF NOT EXISTS Orders (
                OrderId INTEGER PRIMARY KEY, CustomerEmail TEXT NOT NULL,
                OrderedAt TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS OrderItems (
                OrderId INTEGER NOT NULL, Sku TEXT NOT NULL, Quantity INTEGER NOT NULL);

            CREATE TABLE IF NOT EXISTS Returns (
                ReturnId INTEGER PRIMARY KEY, OrderId INTEGER NOT NULL,
                Sku TEXT NOT NULL, Reason TEXT NOT NULL,
                CreatedAt TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Open');

            CREATE TABLE IF NOT EXISTS ReturnAnalysis (
                ReturnId INTEGER PRIMARY KEY, Category TEXT NOT NULL,
                Sentiment TEXT NOT NULL, RootCause TEXT NOT NULL,
                IsProductDefect INTEGER NOT NULL, Confidence REAL NOT NULL,
                Model TEXT NOT NULL, AnalyzedAt TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS ReviewFlags (
                FlagId INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL,
                Reason TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'PendingApproval',
                CreatedAt TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS ReturnSafety (
                ReturnId INTEGER PRIMARY KEY, InjectionSuspected INTEGER NOT NULL,
                GuardHits TEXT NOT NULL, RedactionCount INTEGER NOT NULL,
                NeedsHumanReview INTEGER NOT NULL, CheckedAt TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
    }
}