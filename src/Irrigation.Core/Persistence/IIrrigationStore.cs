using Irrigation.Core.Configuration;
using Irrigation.Core.Model;
using Irrigation.Shared;

namespace Irrigation.Core.Persistence;

public interface IIrrigationStore
{
    Task InitializeAsync(IReadOnlyList<ZoneOptions> configuredZones, CancellationToken cancellationToken);

    Task<IReadOnlyList<IrrigationZone>> GetZonesAsync(CancellationToken cancellationToken);

    Task<IrrigationZone?> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken);

    Task RecordTelemetryAsync(string eventType, string message, Guid? zoneId, CancellationToken cancellationToken);

    Task<Guid> BeginRunAsync(IrrigationZone zone, TimeSpan plannedDuration, CancellationToken cancellationToken);

    Task CompleteRunAsync(Guid runId, string outcome, CancellationToken cancellationToken);

    Task<IReadOnlyList<RunHistoryDto>> GetRunHistoryAsync(int count, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, RunHistoryDto>> GetLastRunsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TelemetryEventDto>> GetTelemetryAsync(int count, CancellationToken cancellationToken);
}
