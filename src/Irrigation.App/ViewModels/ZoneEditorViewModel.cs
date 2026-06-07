using Irrigation.Shared;

namespace Irrigation.App.ViewModels;

public sealed class ZoneEditorViewModel(ZoneStatusDto status) : ViewModelBase
{
    private string name = status.Name;
    private double defaultMinutes = Math.Max(1, status.DefaultDuration.TotalMinutes);
    private bool enabled = status.Enabled;

    public Guid ZoneId { get; } = status.ZoneId;

    public int Order { get; } = status.Order;

    public string Name
    {
        get => this.name;
        set => this.SetProperty(ref this.name, value);
    }

    public double DefaultMinutes
    {
        get => this.defaultMinutes;
        set => this.SetProperty(ref this.defaultMinutes, Math.Max(1, value));
    }

    public bool Enabled
    {
        get => this.enabled;
        set => this.SetProperty(ref this.enabled, value);
    }

    public string ZoneLabel => $"Zone {this.Order}";

    public UpdateZoneRequest ToRequest()
    {
        return new UpdateZoneRequest(this.Name, TimeSpan.FromMinutes(this.DefaultMinutes), this.Enabled);
    }
}

public sealed class RunHistoryItemViewModel(RunHistoryDto history)
{
    public string ZoneName => history.ZoneName;

    public string StartedAt => history.StartedAt.LocalDateTime.ToString("g");

    public string Duration => history.ActualDuration is null
        ? history.PlannedDuration.ToString("mm\\:ss")
        : history.ActualDuration.Value.ToString("mm\\:ss");

    public string Outcome => history.Outcome;
}

public sealed class TelemetryItemViewModel(TelemetryEventDto telemetry)
{
    public string Timestamp => telemetry.Timestamp.LocalDateTime.ToString("g");

    public string EventType => telemetry.EventType;

    public string Message => telemetry.Message;
}

public sealed class WifiNetworkViewModel(WifiNetworkDto network)
{
    public string Ssid => network.Ssid;

    public string Signal => $"{network.SignalPercent}%";

    public bool IsConnected => network.IsConnected;
}
