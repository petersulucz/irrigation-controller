namespace Irrigation.Core.Model;

public sealed record IrrigationZone(
    Guid ZoneId,
    int Order,
    string Name,
    int Pin,
    bool Enabled,
    TimeSpan DefaultDuration);
