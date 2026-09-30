using ReturnsIQ.Data;
using Xunit;

public class DataTests
{
    [Fact]
    public void Seed_and_query_works()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
        var db = new Database(path);
        db.Init();
        Seeder.Seed(db, FindSeed());

        var repo = new ReturnRepository(db);
        var returns = repo.ListReturns("SKU-123", DateTime.MinValue, DateTime.MaxValue);

        //Assert.NotEmpty(returns);
        //Assert.NotNull(repo.GetOrder(1001));

        var from = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(22, repo.ListReturns("SKU-123", from, to).Count);
        Assert.NotNull(repo.GetOrder(1001));
    }

    private static string FindSeed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "seed.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "data", "seed.json");
    }
}