namespace Irrigation.Core.Configuration;

public enum RelayPolarity
{
    ActiveHigh,
    ActiveLow
}

public sealed class IrrigationOptions
{
    public string DataPath { get; set; } = "data";

    public bool SetupComplete { get; set; }

    public TimeSpan MaximumManualDuration { get; set; } = TimeSpan.FromMinutes(60);

    public HardwareOptions Hardware { get; set; } = new();

    public ApiSecurityOptions Security { get; set; } = new();
}

public sealed class ApiSecurityOptions
{
    public string? Token { get; set; }
}

public sealed class HardwareOptions
{
    public string Provider { get; set; } = "Simulated";

    public RelayPolarity RelayPolarity { get; set; } = RelayPolarity.ActiveHigh;

    public List<ZoneOptions> Zones { get; set; } = [];
}

public sealed class ZoneOptions
{
    public Guid? ZoneId { get; set; }

    public int Order { get; set; }

    public string Name { get; set; } = "";

    public int Pin { get; set; }

    public bool Enabled { get; set; } = true;

    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
}
