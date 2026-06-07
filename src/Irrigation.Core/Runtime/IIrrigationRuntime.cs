using Irrigation.Shared;
using Irrigation.Core.Model;

namespace Irrigation.Core.Runtime;

public interface IIrrigationRuntime
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task RunZoneAsync(Guid zoneId, CancellationToken cancellationToken);

    Task RunAllZonesAsync(CancellationToken cancellationToken);

    Task StopAllAsync(CancellationToken cancellationToken);

    Task<IrrigationZone?> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken);

    Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken);
}
