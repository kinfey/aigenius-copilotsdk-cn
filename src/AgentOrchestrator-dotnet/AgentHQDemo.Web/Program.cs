using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AgentHQDemo.Web;
using AgentHQDemo.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5050";
if (!Uri.TryCreate(apiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var apiUri)
    || (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException("ApiBaseUrl 必须是有效的 HTTP 或 HTTPS 地址。");
}
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiUri, Timeout = Timeout.InfiniteTimeSpan });
builder.Services.AddScoped<ChatApi>();
builder.Services.AddScoped<BrowserStorage>();

await builder.Build().RunAsync();
