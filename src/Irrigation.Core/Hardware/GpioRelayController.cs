using Irrigation.Core.Configuration;
using Irrigation.Core.Model;
using Microsoft.Extensions.Options;
using System.Device.Gpio;

namespace Irrigation.Core.Hardware;

public sealed class GpioRelayController : IRelayController, IDisposable
{
    private readonly object gate = new();
    private readonly GpioController controller = new();
    private readonly RelayPolarity polarity;
    private readonly Dictionary<int, bool> logicalPinStates = [];
    private bool disposed;

    public GpioRelayController(IOptions<IrrigationOptions> options)
    {
        this.polarity = options.Value.Hardware.RelayPolarity;
    }

    public Task InitializeOffAsync(IReadOnlyCollection<IrrigationZone> zones, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            foreach (var zone in zones)
            {
                if (!this.controller.IsPinOpen(zone.Pin))
                {
                    this.controller.OpenPin(zone.Pin, PinMode.Output);
                }

                this.WriteLogical(zone.Pin, false);
            }
        }

        return Task.CompletedTask;
    }

    public Task SetZoneAsync(IrrigationZone zone, bool enabled, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (!this.controller.IsPinOpen(zone.Pin))
            {
                this.controller.OpenPin(zone.Pin, PinMode.Output);
            }

            this.WriteLogical(zone.Pin, enabled);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<int, bool> GetPinStates()
    {
        lock (this.gate)
        {
            return this.logicalPinStates.ToDictionary();
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        lock (this.gate)
        {
            foreach (var pin in this.logicalPinStates.Keys.ToList())
            {
                this.WriteLogical(pin, false);
            }
        }

        this.controller.Dispose();
        this.disposed = true;
    }

    private void WriteLogical(int pin, bool enabled)
    {
        var pinValue = this.polarity == RelayPolarity.ActiveHigh
            ? enabled ? PinValue.High : PinValue.Low
            : enabled ? PinValue.Low : PinValue.High;

        this.controller.Write(pin, pinValue);
        this.logicalPinStates[pin] = enabled;
    }
}
