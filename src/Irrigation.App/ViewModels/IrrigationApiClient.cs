using System.Net.Http.Headers;
using System.Net.Http.Json;
using Irrigation.Shared;

namespace Irrigation.App.ViewModels;

public sealed class IrrigationApiClient
{
    private readonly HttpClient client;

    public IrrigationApiClient()
    {
        this.client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("IRRIGATION_API_URL") ?? "http://localhost:5030")
        };
        this.client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Environment.GetEnvironmentVariable("IRRIGATION_API_TOKEN") ?? "dev-token");
    }

    public async Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        return await this.client.GetFromJsonAsync<SystemStatusDto>("/api/system/status", cancellationToken)
            ?? throw new InvalidOperationException("The irrigation API returned an empty status response.");
    }

    public async Task<ZoneDto> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        using var response = await this.client.PutAsJsonAsync($"/api/zones/{zoneId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ZoneDto>(cancellationToken)
            ?? throw new InvalidOperationException("The irrigation API returned an empty zone response.");
    }

    public async Task<IReadOnlyList<RunHistoryDto>> GetRunHistoryAsync(CancellationToken cancellationToken)
    {
        return await this.client.GetFromJsonAsync<IReadOnlyList<RunHistoryDto>>("/api/metrics/run-history?count=100", cancellationToken)
            ?? [];
    }

    public async Task<IReadOnlyList<TelemetryEventDto>> GetTelemetryAsync(CancellationToken cancellationToken)
    {
        return await this.client.GetFromJsonAsync<IReadOnlyList<TelemetryEventDto>>("/api/telemetry?count=100", cancellationToken)
            ?? [];
    }

    public async Task<WifiStatusDto> GetWifiStatusAsync(CancellationToken cancellationToken)
    {
        return await this.client.GetFromJsonAsync<WifiStatusDto>("/api/network/status", cancellationToken)
            ?? new WifiStatusDto(false, false, null, "No Wi-Fi status returned.");
    }

    public async Task<IReadOnlyList<WifiNetworkDto>> ScanWifiAsync(CancellationToken cancellationToken)
    {
        return await this.client.GetFromJsonAsync<IReadOnlyList<WifiNetworkDto>>("/api/network/wifi/scan", cancellationToken)
            ?? [];
    }

    public async Task<WifiStatusDto> ConnectWifiAsync(WifiConnectRequest request, CancellationToken cancellationToken)
    {
        using var response = await this.client.PostAsJsonAsync("/api/network/wifi/connect", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WifiStatusDto>(cancellationToken)
            ?? new WifiStatusDto(false, false, null, "No Wi-Fi status returned.");
    }

    public async Task RunZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        using var response = await this.client.PostAsync($"/api/zones/{zoneId}/run", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RunAllAsync(CancellationToken cancellationToken)
    {
        using var response = await this.client.PostAsync("/api/zones/run-all", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        using var response = await this.client.PostAsync("/api/system/stop-all", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
