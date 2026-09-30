namespace ReturnsIQ.Data;

public static class RepoPaths
{
    public static string DataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "seed.json")))
            dir = dir.Parent;
        return dir is null
            ? throw new DirectoryNotFoundException("Could not find data/seed.json")
            : Path.Combine(dir.FullName, "data");
    }
}