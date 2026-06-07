using Irrigation.Core.Configuration;
using Irrigation.Core.Hardware;
using Irrigation.Core.Persistence;
using Irrigation.Core.Runtime;
using Irrigation.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<IrrigationOptions>(builder.Configuration.GetSection("Irrigation"));
builder.Services.AddOpenApi();
var irrigationOptions = builder.Configuration.GetSection("Irrigation").Get<IrrigationOptions>() ?? new IrrigationOptions();
Directory.CreateDirectory(irrigationOptions.DataPath);
var databasePath = Path.Combine(irrigationOptions.DataPath, "irrigation.db");
builder.Services.AddDbContextFactory<IrrigationDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton<IRelayController>(services =>
{
    var options = services.GetRequiredService<IOptions<IrrigationOptions>>().Value;
    return options.Hardware.Provider.Equals("RaspberryPiGpio", StringComparison.OrdinalIgnoreCase) ||
        options.Hardware.Provider.Equals("Gpio", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<GpioRelayController>(services)
        : ActivatorUtilities.CreateInstance<SimulatedRelayController>(services);
});
builder.Services.AddSingleton<IIrrigationStore, EfIrrigationStore>();
builder.Services.AddSingleton<IIrrigationRuntime, IrrigationRuntime>();
builder.Services.AddHostedService<IrrigationStartupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api");

api.MapGet("/setup/status", (IIrrigationRuntime runtime) =>
{
    var status = runtime.GetStatusAsync(CancellationToken.None).GetAwaiter().GetResult();
    return new SetupStatusDto(status.SetupComplete, status.Zones.Count);
});

api.MapGet("/system/status", async (IIrrigationRuntime runtime, CancellationToken token) => await runtime.GetStatusAsync(token))
    .RequireIrrigationToken();

api.MapPost("/system/stop-all", async (IIrrigationRuntime runtime, CancellationToken token) =>
{
    await runtime.StopAllAsync(token);
    return Results.NoContent();
}).RequireIrrigationToken();

api.MapGet("/zones", async (IIrrigationRuntime runtime, CancellationToken token) => (await runtime.GetStatusAsync(token)).Zones)
    .RequireIrrigationToken();

api.MapPut("/zones/{zoneId:guid}", async (Guid zoneId, UpdateZoneRequest request, IIrrigationRuntime runtime, CancellationToken token) =>
{
    var updated = await runtime.UpdateZoneAsync(zoneId, request, token);
    return updated is null
        ? Results.NotFound()
        : Results.Ok(new ZoneDto(updated.ZoneId, updated.Order, updated.Name, updated.DefaultDuration, updated.Enabled, updated.Pin));
}).RequireIrrigationToken();

api.MapPost("/zones/{zoneId:guid}/run", async (Guid zoneId, IIrrigationRuntime runtime, CancellationToken token) =>
{
    try
    {
        await runtime.RunZoneAsync(zoneId, token);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireIrrigationToken();

api.MapPost("/zones/run-all", async (IIrrigationRuntime runtime, CancellationToken token) =>
{
    await runtime.RunAllZonesAsync(token);
    return Results.NoContent();
}).RequireIrrigationToken();

api.MapGet("/telemetry", async (int? count, IIrrigationStore store, CancellationToken token) =>
    await store.GetTelemetryAsync(count is > 0 and <= 500 ? count.Value : 100, token))
    .RequireIrrigationToken();

api.MapGet("/metrics/run-history", async (int? count, IIrrigationStore store, CancellationToken token) =>
    await store.GetRunHistoryAsync(count is > 0 and <= 500 ? count.Value : 100, token))
    .RequireIrrigationToken();

api.MapGet("/network/status", () =>
    new WifiStatusDto(true, true, "Simulated Wi-Fi", "Local simulation mode. NetworkManager integration is not active on this machine."))
    .RequireIrrigationToken();

api.MapGet("/network/wifi/scan", () => new[]
{
    new WifiNetworkDto("Home Network", 92, false),
    new WifiNetworkDto("Garden Controller Lab", 78, true),
    new WifiNetworkDto("Guest", 54, false)
})
    .RequireIrrigationToken();

api.MapPost("/network/wifi/connect", (WifiConnectRequest request) =>
    Results.Ok(new WifiStatusDto(true, true, request.Ssid, $"Simulated connection to {request.Ssid}.")))
    .RequireIrrigationToken();

app.Run();

internal static class IrrigationEndpointConventions
{
    public static RouteHandlerBuilder RequireIrrigationToken(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<IrrigationOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.Security.Token))
            {
                return Results.Problem("API token is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var header = context.HttpContext.Request.Headers.Authorization.ToString();
            var expected = $"Bearer {options.Security.Token}";
            if (!string.Equals(header, expected, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            return await next(context);
        });
    }
}

internal sealed class IrrigationStartupService(IIrrigationRuntime runtime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => runtime.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => runtime.StopAllAsync(cancellationToken);
}
