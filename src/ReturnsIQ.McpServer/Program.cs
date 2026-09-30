using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReturnsIQ.Data;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for MCP messages, so send all logs to stderr
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var dbPath = Environment.GetEnvironmentVariable("RETURNSIQ_DB")
             ?? Path.Combine(RepoPaths.DataDir(), "returnsiq.db");
if (!File.Exists(dbPath))
    throw new FileNotFoundException($"Database not found at {dbPath}. Run the Agent seed command first.");

var db = new Database(dbPath);
db.Init();
builder.Services.AddSingleton(db);
builder.Services.AddSingleton<ReturnRepository>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();