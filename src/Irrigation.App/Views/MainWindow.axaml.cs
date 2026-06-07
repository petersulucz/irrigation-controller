using Avalonia.Controls;
using Avalonia.Interactivity;
using Irrigation.App.ViewModels;

namespace Irrigation.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
    }

    private async void RunZoneClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ZoneButtonViewModel zone } ||
            this.DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        await vm.RunZoneAsync(zone.ZoneId);
    }

    private void HomeClicked(object? sender, RoutedEventArgs e)
    {
        (this.DataContext as MainWindowViewModel)?.Navigate("Home");
    }

    private void SettingsClicked(object? sender, RoutedEventArgs e)
    {
        (this.DataContext as MainWindowViewModel)?.Navigate("Settings");
    }

    private void WifiClicked(object? sender, RoutedEventArgs e)
    {
        (this.DataContext as MainWindowViewModel)?.Navigate("Wi-Fi");
    }

    private void MetricsClicked(object? sender, RoutedEventArgs e)
    {
        (this.DataContext as MainWindowViewModel)?.Navigate("Metrics");
    }

    private async void SaveZoneClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ZoneEditorViewModel zone } &&
            this.DataContext is MainWindowViewModel vm)
        {
            await vm.SaveZoneAsync(zone);
        }
    }

    private async void ConnectWifiClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WifiNetworkViewModel network } &&
            this.DataContext is MainWindowViewModel vm)
        {
            await vm.ConnectWifiAsync(network);
        }
    }
}
