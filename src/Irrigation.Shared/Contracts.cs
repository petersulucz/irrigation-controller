namespace Irrigation.Shared;

public enum ZoneVisualState
{
    Disabled,
    Idle,
    Queued,
    Running,
    Error
}

public sealed record ZoneDto(
    Guid ZoneId,
    int Order,
    string Name,
    TimeSpan DefaultDuration,
    bool Enabled,
    int? Pin = null);

public sealed record UpdateZoneRequest(
    string Name,
    TimeSpan DefaultDuration,
    bool Enabled);

public sealed record ZoneStatusDto(
    Guid ZoneId,
    int Order,
    string Name,
    TimeSpan DefaultDuration,
    bool Enabled,
    ZoneVisualState State,
    TimeSpan? Remaining,
    int? QueuePosition,
    DateTimeOffset? LastRunStartedAt = null,
    DateTimeOffset? LastRunCompletedAt = null,
    TimeSpan? LastRunDuration = null,
    int? Pin = null);

public sealed record QueuedZoneDto(
    Guid ZoneId,
    string Name,
    TimeSpan Duration,
    int QueuePosition);

public sealed record SystemStatusDto(
    DateTimeOffset ServerTime,
    bool SetupComplete,
    Guid? ActiveZoneId,
    IReadOnlyList<ZoneStatusDto> Zones,
    IReadOnlyList<QueuedZoneDto> Queue,
    IReadOnlyDictionary<int, bool> SimulatedPins);

public sealed record TelemetryEventDto(
    DateTimeOffset Timestamp,
    string EventType,
    string Message,
    Guid? ZoneId = null);

public sealed record RunHistoryDto(
    Guid RunId,
    Guid ZoneId,
    string ZoneName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan PlannedDuration,
    TimeSpan? ActualDuration,
    string Outcome);

public sealed record SetupStatusDto(bool IsComplete, int ConfiguredZoneCount);

public sealed record WifiNetworkDto(string Ssid, int SignalPercent, bool IsConnected);

public sealed record WifiStatusDto(bool IsAvailable, bool IsConnected, string? ConnectedSsid, string Message);

public sealed record WifiConnectRequest(string Ssid, string Passphrase);

public sealed record ApiTokenDto(string Token, DateTimeOffset CreatedAt);
