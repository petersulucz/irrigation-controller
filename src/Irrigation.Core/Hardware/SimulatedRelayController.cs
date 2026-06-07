using Irrigation.Core.Model;

namespace Irrigation.Core.Hardware;

public sealed class SimulatedRelayController : IRelayController
{
    private readonly object gate = new();
    private readonly Dictionary<int, bool> pinStates = [];

    public Task InitializeOffAsync(IReadOnlyCollection<IrrigationZone> zones, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            foreach (var zone in zones)
            {
                this.pinStates[zone.Pin] = false;
            }
        }

        return Task.CompletedTask;
    }

    public Task SetZoneAsync(IrrigationZone zone, bool enabled, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.pinStates[zone.Pin] = enabled;
        }

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<int, bool> GetPinStates()
    {
        lock (this.gate)
        {
            return this.pinStates.ToDictionary();
        }
    }
}
