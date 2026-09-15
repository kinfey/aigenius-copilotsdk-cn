using AgentHQDemo.Api.Services;
using AgentHQDemo.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5050");
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5051").WithHeaders("Content-Type").WithMethods("GET", "POST", "DELETE")));
var dbPath = Path.GetFullPath(builder.Configuration["Retail:DatabasePath"] ?? "retail.db", builder.Environment.ContentRootPath);
builder.Configuration["Retail:DatabasePath"] = dbPath;
builder.Services.AddDbContext<RetailDbContext>(options =>
    options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()));
builder.Services.AddScoped<RetailAnalyticsService>();
builder.Services.AddSingleton<IChatService, CopilotChatService>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<RetailDbContext>().InitializeAsync();
}
await app.RunAsync();

public partial class Program;
