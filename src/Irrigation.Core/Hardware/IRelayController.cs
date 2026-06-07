using Irrigation.Core.Model;

namespace Irrigation.Core.Hardware;

public interface IRelayController
{
    Task InitializeOffAsync(IReadOnlyCollection<IrrigationZone> zones, CancellationToken cancellationToken);

    Task SetZoneAsync(IrrigationZone zone, bool enabled, CancellationToken cancellationToken);

    IReadOnlyDictionary<int, bool> GetPinStates();
}
