using System.Collections.ObjectModel;
using Avalonia.Threading;
using Irrigation.Shared;

namespace Irrigation.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IrrigationApiClient apiClient;
    private readonly DispatcherTimer timer;
    private string statusText = "Starting irrigation engine...";
    private string activeZoneText = "Waiting for backend engine";
    private string selectedPage = "Home";
    private string wifiStatusText = "Not loaded";
    private string wifiPassphrase = "";
    private bool isBusy;
    private bool isConnected;

    public MainWindowViewModel(IrrigationApiClient apiClient)
    {
        this.apiClient = apiClient;
        this.RunAllCommand = new RelayCommand(this.RunAllAsync);
        this.StopAllCommand = new RelayCommand(this.StopAllAsync);
        this.RefreshCommand = new RelayCommand(this.RefreshAsync);
        this.ScanWifiCommand = new RelayCommand(this.ScanWifiAsync);

        this.timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        this.timer.Tick += async (_, _) => await this.RefreshAsync();
        this.timer.Start();
        _ = this.RefreshAsync();
    }

    public ObservableCollection<ZoneButtonViewModel> Zones { get; } = [];

    public ObservableCollection<ZoneEditorViewModel> ZoneEditors { get; } = [];

    public ObservableCollection<RunHistoryItemViewModel> RunHistory { get; } = [];

    public ObservableCollection<TelemetryItemViewModel> Telemetry { get; } = [];

    public ObservableCollection<WifiNetworkViewModel> WifiNetworks { get; } = [];

    public RelayCommand RunAllCommand { get; }

    public RelayCommand StopAllCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ScanWifiCommand { get; }

    public string StatusText
    {
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText, value);
    }

    public string ActiveZoneText
    {
        get => this.activeZoneText;
        private set => this.SetProperty(ref this.activeZoneText, value);
    }

    public string SelectedPage
    {
        get => this.selectedPage;
        private set
        {
            if (this.SetProperty(ref this.selectedPage, value))
            {
                this.OnPropertyChanged(nameof(this.IsHomeVisible));
                this.OnPropertyChanged(nameof(this.IsSettingsVisible));
                this.OnPropertyChanged(nameof(this.IsWifiVisible));
                this.OnPropertyChanged(nameof(this.IsMetricsVisible));
            }
        }
    }

    public string WifiStatusText
    {
        get => this.wifiStatusText;
        private set => this.SetProperty(ref this.wifiStatusText, value);
    }

    public string WifiPassphrase
    {
        get => this.wifiPassphrase;
        set => this.SetProperty(ref this.wifiPassphrase, value);
    }

    public bool IsBusy
    {
        get => this.isBusy;
        private set => this.SetProperty(ref this.isBusy, value);
    }

    public bool IsConnected
    {
        get => this.isConnected;
        private set
        {
            if (this.SetProperty(ref this.isConnected, value))
            {
                this.OnPropertyChanged(nameof(this.IsStartingVisible));
                this.OnPropertyChanged(nameof(this.IsShellVisible));
            }
        }
    }

    public bool IsStartingVisible => !this.IsConnected;

    public bool IsShellVisible => this.IsConnected;

    public bool IsHomeVisible => this.SelectedPage == "Home";

    public bool IsSettingsVisible => this.SelectedPage == "Settings";

    public bool IsWifiVisible => this.SelectedPage == "Wi-Fi";

    public bool IsMetricsVisible => this.SelectedPage == "Metrics";

    public void Navigate(string page)
    {
        this.SelectedPage = page;
        if (page == "Metrics")
        {
            _ = this.RefreshMetricsAsync();
        }
        else if (page == "Wi-Fi")
        {
            _ = this.RefreshWifiAsync();
        }
    }

    public async Task RunZoneAsync(Guid zoneId)
    {
        await this.ExecuteApiCallAsync(async token => await this.apiClient.RunZoneAsync(zoneId, token));
    }

    public async Task SaveZoneAsync(ZoneEditorViewModel zone)
    {
        await this.ExecuteApiCallAsync(async token => await this.apiClient.UpdateZoneAsync(zone.ZoneId, zone.ToRequest(), token));
    }

    public async Task ConnectWifiAsync(WifiNetworkViewModel network)
    {
        await this.ExecuteApiCallAsync(async token =>
        {
            var status = await this.apiClient.ConnectWifiAsync(new WifiConnectRequest(network.Ssid, this.WifiPassphrase), token);
            this.WifiStatusText = status.Message;
        });
        await this.RefreshWifiAsync();
    }

    private async Task RunAllAsync()
    {
        await this.ExecuteApiCallAsync(this.apiClient.RunAllAsync);
    }

    private async Task StopAllAsync()
    {
        await this.ExecuteApiCallAsync(this.apiClient.StopAllAsync);
    }

    private async Task ExecuteApiCallAsync(Func<CancellationToken, Task> action)
    {
        this.IsBusy = true;
        try
        {
            await action(CancellationToken.None);
            await this.RefreshAsync();
        }
        catch
        {
            this.IsConnected = false;
            this.StatusText = "Starting irrigation engine...";
            this.ActiveZoneText = "Waiting for backend engine";
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var status = await this.apiClient.GetStatusAsync(CancellationToken.None);
            this.IsConnected = true;
            this.ApplyStatus(status);
            this.StatusText = status.SetupComplete
                ? "System ready"
                : "Setup not complete - running in local simulation";
        }
        catch
        {
            this.IsConnected = false;
            this.StatusText = "Starting irrigation engine...";
            this.ActiveZoneText = "Waiting for backend engine";
        }
    }

    private async Task RefreshMetricsAsync()
    {
        if (!this.IsConnected)
        {
            return;
        }

        try
        {
            var history = await this.apiClient.GetRunHistoryAsync(CancellationToken.None);
            var telemetry = await this.apiClient.GetTelemetryAsync(CancellationToken.None);
            Replace(this.RunHistory, history.Select(h => new RunHistoryItemViewModel(h)));
            Replace(this.Telemetry, telemetry.Select(t => new TelemetryItemViewModel(t)));
        }
        catch
        {
            this.IsConnected = false;
        }
    }

    private async Task RefreshWifiAsync()
    {
        if (!this.IsConnected)
        {
            return;
        }

        try
        {
            var status = await this.apiClient.GetWifiStatusAsync(CancellationToken.None);
            this.WifiStatusText = status.Message;
            await this.ScanWifiAsync();
        }
        catch
        {
            this.IsConnected = false;
        }
    }

    private async Task ScanWifiAsync()
    {
        try
        {
            var networks = await this.apiClient.ScanWifiAsync(CancellationToken.None);
            Replace(this.WifiNetworks, networks.Select(n => new WifiNetworkViewModel(n)));
        }
        catch
        {
            this.IsConnected = false;
        }
    }

    private void ApplyStatus(SystemStatusDto status)
    {
        foreach (var zone in status.Zones.OrderBy(z => z.Order))
        {
            var existing = this.Zones.FirstOrDefault(z => z.ZoneId == zone.ZoneId);
            if (existing is null)
            {
                this.Zones.Add(new ZoneButtonViewModel(zone));
            }
            else
            {
                existing.Update(zone);
            }
        }

        this.SyncEditors(status.Zones);

        var active = status.Zones.FirstOrDefault(z => z.State == ZoneVisualState.Running);
        this.ActiveZoneText = active is null
            ? "No zone running"
            : $"{active.Name} is watering - {active.Remaining:mm\\:ss} remaining";
    }

    private void SyncEditors(IReadOnlyList<ZoneStatusDto> zones)
    {
        foreach (var zone in zones.OrderBy(z => z.Order))
        {
            if (this.ZoneEditors.All(z => z.ZoneId != zone.ZoneId))
            {
                this.ZoneEditors.Add(new ZoneEditorViewModel(zone));
            }
        }
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
