using AgentHQDemo.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
var dbPath = Path.GetFullPath(builder.Configuration["Retail:DatabasePath"] ?? "retail.db");
if (!File.Exists(dbPath))
    throw new FileNotFoundException("Start the API first to create the retail database.", dbPath);
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadOnly
}.ToString();
builder.Services.AddDbContext<RetailDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<RetailAnalyticsService>();
builder.Services.AddMcpServer(options => options.ServerInfo = new() { Name = "retail-analytics", Version = "1.0.0" })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
