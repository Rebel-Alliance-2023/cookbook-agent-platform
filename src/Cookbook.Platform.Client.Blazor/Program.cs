using Cookbook.Platform.Client.Blazor.Components;
using Cookbook.Platform.Client.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add client services
builder.Services.AddScoped<SignalRClientService>();
builder.Services.AddScoped<ApiClientService>();

// Add HTTP client for Gateway API
builder.Services.AddHttpClient("GatewayApi", client =>
{
    client.BaseAddress = new Uri("http://gateway");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
