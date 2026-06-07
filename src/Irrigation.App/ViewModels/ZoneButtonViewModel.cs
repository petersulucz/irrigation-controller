using Irrigation.Shared;

namespace Irrigation.App.ViewModels;

public sealed class ZoneButtonViewModel : ViewModelBase
{
    private ZoneStatusDto status;

    public ZoneButtonViewModel(ZoneStatusDto status)
    {
        this.status = status;
    }

    public Guid ZoneId => this.status.ZoneId;

    public string Name => this.status.Name;

    public string Duration => $"{this.status.DefaultDuration.TotalMinutes:0} min";

    public string StateText => this.status.State switch
    {
        ZoneVisualState.Running => this.status.Remaining is null ? "Running" : $"Running - {this.status.Remaining.Value:mm\\:ss}",
        ZoneVisualState.Queued => $"Queued #{this.status.QueuePosition}",
        ZoneVisualState.Disabled => "Disabled",
        ZoneVisualState.Error => "Error",
        _ => "Tap to run"
    };

    public string LastRunText => this.status.LastRunStartedAt is null
        ? "Last run: Never"
        : $"Last run: {this.status.LastRunStartedAt.Value.LocalDateTime:g}";

    public bool IsRunning => this.status.State == ZoneVisualState.Running;

    public bool IsQueued => this.status.State == ZoneVisualState.Queued;

    public bool IsDisabled => this.status.State == ZoneVisualState.Disabled;

    public bool IsError => this.status.State == ZoneVisualState.Error;

    public bool CanRun => this.status.Enabled;

    public void Update(ZoneStatusDto updatedStatus)
    {
        this.status = updatedStatus;
        this.OnPropertyChanged(nameof(this.Name));
        this.OnPropertyChanged(nameof(this.Duration));
        this.OnPropertyChanged(nameof(this.StateText));
        this.OnPropertyChanged(nameof(this.LastRunText));
        this.OnPropertyChanged(nameof(this.IsRunning));
        this.OnPropertyChanged(nameof(this.IsQueued));
        this.OnPropertyChanged(nameof(this.IsDisabled));
        this.OnPropertyChanged(nameof(this.IsError));
        this.OnPropertyChanged(nameof(this.CanRun));
    }
}
