using System.Diagnostics;
using Cookbook.Platform.Client.Blazor.Components;
using Cookbook.Platform.Client.Blazor.Services;

// Force debugger attach for troubleshooting
if (Debugger.IsAttached == false)
{
    Debugger.Launch();
}

// Use a bootstrap logger to capture startup issues before DI is configured
var bootstrapLoggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Startup");

try
{
    bootstrapLogger.LogInformation("Blazor Client: Creating WebApplication builder...");

    var builder = WebApplication.CreateBuilder(args);

    bootstrapLogger.LogInformation("Blazor Client: Adding service defaults...");
    // Add Aspire service defaults (includes OpenTelemetry logging)
    builder.AddServiceDefaults();

    bootstrapLogger.LogInformation("Blazor Client: Adding Razor components...");
    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    bootstrapLogger.LogInformation("Blazor Client: Adding client services...");
    // Add client services
    builder.Services.AddScoped<SignalRClientService>();
    builder.Services.AddScoped<ApiClientService>();

    bootstrapLogger.LogInformation("Blazor Client: Adding HTTP client...");
    // Add HTTP client for Gateway API
    builder.Services.AddHttpClient("GatewayApi", client =>
    {
        client.BaseAddress = new Uri("http://gateway");
    });

    bootstrapLogger.LogInformation("Blazor Client: Building application...");
    var app = builder.Build();

    // Get logger from DI after building
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    logger.LogInformation("Blazor Client application built successfully");

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

    logger.LogInformation("Blazor Client configured and ready to start");

    app.Run();
}
catch (Exception ex)
{
    bootstrapLogger.LogCritical(ex, "Blazor Client startup failed with exception");
    throw;
}
finally
{
    bootstrapLoggerFactory.Dispose();
}
