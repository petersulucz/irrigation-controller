using Irrigation.Core.Configuration;
using Irrigation.Core.Hardware;
using Irrigation.Core.Model;
using Irrigation.Core.Persistence;
using Irrigation.Shared;
using Microsoft.Extensions.Options;

namespace Irrigation.Core.Runtime;

public sealed class IrrigationRuntime : IIrrigationRuntime, IAsyncDisposable
{
    private sealed record QueueItem(IrrigationZone Zone, TimeSpan Duration);

    private readonly object gate = new();
    private readonly IRelayController relayController;
    private readonly IIrrigationStore store;
    private readonly IrrigationOptions options;
    private readonly SemaphoreSlim signal = new(0);
    private readonly CancellationTokenSource disposeCts = new();
    private Task? worker;

    private List<IrrigationZone> zones = [];
    private QueueItem? active;
    private Guid? activeRunId;
    private DateTimeOffset? activeStartedAt;
    private DateTimeOffset? activeEndsAt;
    private List<QueueItem> pending = [];

    public IrrigationRuntime(
        IOptions<IrrigationOptions> options,
        IRelayController relayController,
        IIrrigationStore store)
    {
        this.options = options.Value;
        this.relayController = relayController;
        this.store = store;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var configuredZones = this.options.Hardware.Zones.Count == 0
            ? DefaultZones()
            : this.options.Hardware.Zones;

        await this.store.InitializeAsync(configuredZones, cancellationToken);
        var loadedZones = (await this.store.GetZonesAsync(cancellationToken)).ToList();
        lock (this.gate)
        {
            this.zones = loadedZones;
        }

        await this.relayController.InitializeOffAsync(loadedZones, cancellationToken);
        await this.store.RecordTelemetryAsync("system.startup", "All configured zones initialized off.", null, cancellationToken);
        this.worker ??= Task.Run(() => this.RunWorkerAsync(this.disposeCts.Token), CancellationToken.None);
    }

    public async Task RunZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await this.FindEnabledZoneAsync(zoneId, cancellationToken);
        if (zone is null)
        {
            await this.store.RecordTelemetryAsync("command.rejected", "Zone was not found or is disabled.", zoneId, cancellationToken);
            throw new InvalidOperationException("Zone was not found or is disabled.");
        }

        await this.ReplaceQueueAsync([new QueueItem(zone, ClampDuration(zone.DefaultDuration))], "manual.run-zone", cancellationToken);
    }

    public async Task RunAllZonesAsync(CancellationToken cancellationToken)
    {
        var zoneSnapshot = await this.RefreshZonesAsync(cancellationToken);
        var items = zoneSnapshot
            .Where(z => z.Enabled)
            .OrderBy(z => z.Order)
            .Select(z => new QueueItem(z, ClampDuration(z.DefaultDuration)))
            .ToList();

        await this.ReplaceQueueAsync(items, "manual.run-all", cancellationToken);
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        List<IrrigationZone> zonesToStop;
        Guid? runToStop;
        lock (this.gate)
        {
            runToStop = this.activeRunId;
            this.active = null;
            this.activeRunId = null;
            this.activeStartedAt = null;
            this.activeEndsAt = null;
            this.pending = [];
            zonesToStop = this.zones.ToList();
        }

        foreach (var zone in zonesToStop)
        {
            await this.relayController.SetZoneAsync(zone, false, cancellationToken);
        }

        if (runToStop is not null)
        {
            await this.store.CompleteRunAsync(runToStop.Value, "Stopped", cancellationToken);
        }

        await this.store.RecordTelemetryAsync("manual.stop-all", "All zones stopped and queue cleared.", null, cancellationToken);
        this.ReleaseSignal();
    }

    public async Task<IrrigationZone?> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        var updated = await this.store.UpdateZoneAsync(zoneId, request, cancellationToken);
        if (updated is null)
        {
            return null;
        }

        await this.store.RecordTelemetryAsync("settings.zone-updated", $"Updated {updated.Name}.", zoneId, cancellationToken);
        await this.RefreshZonesAsync(cancellationToken);
        return updated;
    }

    public async Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var lastRuns = await this.store.GetLastRunsAsync(cancellationToken);
        QueueItem? activeSnapshot;
        DateTimeOffset? endsAtSnapshot;
        List<QueueItem> pendingSnapshot;
        List<IrrigationZone> zoneSnapshot;

        lock (this.gate)
        {
            activeSnapshot = this.active;
            endsAtSnapshot = this.activeEndsAt;
            pendingSnapshot = this.pending.ToList();
            zoneSnapshot = this.zones.ToList();
        }

        var now = DateTimeOffset.UtcNow;
        var zoneStatuses = zoneSnapshot.Select(zone =>
        {
            var queueIndex = pendingSnapshot.FindIndex(item => item.Zone.ZoneId == zone.ZoneId);
            var isActive = activeSnapshot?.Zone.ZoneId == zone.ZoneId;
            var state = !zone.Enabled
                ? ZoneVisualState.Disabled
                : isActive
                    ? ZoneVisualState.Running
                    : queueIndex >= 0
                        ? ZoneVisualState.Queued
                        : ZoneVisualState.Idle;

            var remaining = isActive && endsAtSnapshot is not null
                ? endsAtSnapshot.Value - now
                : (TimeSpan?)null;

            lastRuns.TryGetValue(zone.ZoneId, out var lastRun);
            return new ZoneStatusDto(
                zone.ZoneId,
                zone.Order,
                zone.Name,
                zone.DefaultDuration,
                zone.Enabled,
                state,
                remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining,
                queueIndex >= 0 ? queueIndex + 1 : null,
                lastRun?.StartedAt,
                lastRun?.CompletedAt,
                lastRun?.ActualDuration,
                zone.Pin);
        }).ToList();

        var queue = pendingSnapshot.Select((item, index) =>
            new QueuedZoneDto(item.Zone.ZoneId, item.Zone.Name, item.Duration, index + 1)).ToList();

        return new SystemStatusDto(
            now,
            this.options.SetupComplete,
            activeSnapshot?.Zone.ZoneId,
            zoneStatuses,
            queue,
            this.relayController.GetPinStates());
    }

    public async ValueTask DisposeAsync()
    {
        await this.StopAllAsync(CancellationToken.None);
        await this.disposeCts.CancelAsync();
        if (this.worker is not null)
        {
            try
            {
                await this.worker;
            }
            catch (OperationCanceledException)
            {
            }
        }

        this.signal.Dispose();
        this.disposeCts.Dispose();
    }

    private async Task ReplaceQueueAsync(IReadOnlyList<QueueItem> items, string eventType, CancellationToken cancellationToken)
    {
        List<IrrigationZone> zonesToStop;
        Guid? runToReplace;
        QueueItem? startNow;

        lock (this.gate)
        {
            zonesToStop = this.zones.ToList();
            runToReplace = this.activeRunId;
            this.active = null;
            this.activeRunId = null;
            this.activeStartedAt = null;
            this.activeEndsAt = null;

            startNow = items.FirstOrDefault();
            this.pending = items.Skip(1).ToList();
        }

        foreach (var zone in zonesToStop)
        {
            await this.relayController.SetZoneAsync(zone, false, cancellationToken);
        }

        if (runToReplace is not null)
        {
            await this.store.CompleteRunAsync(runToReplace.Value, "Replaced", cancellationToken);
        }

        if (startNow is not null)
        {
            var runId = await this.store.BeginRunAsync(startNow.Zone, startNow.Duration, cancellationToken);
            lock (this.gate)
            {
                this.active = startNow;
                this.activeRunId = runId;
                this.activeStartedAt = DateTimeOffset.UtcNow;
                this.activeEndsAt = this.activeStartedAt.Value + startNow.Duration;
            }

            await this.relayController.SetZoneAsync(startNow.Zone, true, cancellationToken);
            await this.store.RecordTelemetryAsync(eventType, $"Started {startNow.Zone.Name} for {startNow.Duration}.", startNow.Zone.ZoneId, cancellationToken);
        }
        else
        {
            await this.store.RecordTelemetryAsync(eventType, "No enabled zones were available to run.", null, cancellationToken);
        }

        this.ReleaseSignal();
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan delay;
            lock (this.gate)
            {
                delay = this.activeEndsAt is null
                    ? Timeout.InfiniteTimeSpan
                    : this.activeEndsAt.Value - DateTimeOffset.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }
            }

            try
            {
                if (delay == Timeout.InfiniteTimeSpan)
                {
                    await this.signal.WaitAsync(cancellationToken);
                }
                else
                {
                    await Task.WhenAny(Task.Delay(delay, cancellationToken), this.signal.WaitAsync(cancellationToken));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await this.AdvanceIfExpiredAsync(cancellationToken);
        }
    }

    private async Task AdvanceIfExpiredAsync(CancellationToken cancellationToken)
    {
        QueueItem? itemToStop = null;
        QueueItem? itemToStart = null;
        Guid? runToComplete = null;

        lock (this.gate)
        {
            if (this.active is null || this.activeEndsAt > DateTimeOffset.UtcNow)
            {
                return;
            }

            itemToStop = this.active;
            runToComplete = this.activeRunId;
            this.active = null;
            this.activeRunId = null;
            this.activeStartedAt = null;
            this.activeEndsAt = null;

            if (this.pending.Count > 0)
            {
                itemToStart = this.pending[0];
                this.pending.RemoveAt(0);
            }
        }

        await this.relayController.SetZoneAsync(itemToStop.Zone, false, cancellationToken);
        if (runToComplete is not null)
        {
            await this.store.CompleteRunAsync(runToComplete.Value, "Completed", cancellationToken);
        }

        await this.store.RecordTelemetryAsync("runtime.zone-completed", $"Completed {itemToStop.Zone.Name}.", itemToStop.Zone.ZoneId, cancellationToken);

        if (itemToStart is not null)
        {
            var runId = await this.store.BeginRunAsync(itemToStart.Zone, itemToStart.Duration, cancellationToken);
            lock (this.gate)
            {
                this.active = itemToStart;
                this.activeRunId = runId;
                this.activeStartedAt = DateTimeOffset.UtcNow;
                this.activeEndsAt = this.activeStartedAt.Value + itemToStart.Duration;
            }

            await this.relayController.SetZoneAsync(itemToStart.Zone, true, cancellationToken);
            await this.store.RecordTelemetryAsync("runtime.zone-started", $"Started queued zone {itemToStart.Zone.Name}.", itemToStart.Zone.ZoneId, cancellationToken);
        }

        this.ReleaseSignal();
    }

    private async Task<IrrigationZone?> FindEnabledZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zoneSnapshot = await this.RefreshZonesAsync(cancellationToken);
        return zoneSnapshot.FirstOrDefault(z => z.ZoneId == zoneId && z.Enabled);
    }

    private async Task<IReadOnlyList<IrrigationZone>> RefreshZonesAsync(CancellationToken cancellationToken)
    {
        var loadedZones = (await this.store.GetZonesAsync(cancellationToken)).ToList();
        lock (this.gate)
        {
            this.zones = loadedZones;
            return this.zones.ToList();
        }
    }

    private TimeSpan ClampDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return TimeSpan.FromMinutes(5);
        }

        return duration > this.options.MaximumManualDuration
            ? this.options.MaximumManualDuration
            : duration;
    }

    private void ReleaseSignal()
    {
        try
        {
            this.signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private static List<ZoneOptions> DefaultZones()
    {
        return
        [
            new() { Order = 1, Name = "Zone 1", Pin = 4 },
            new() { Order = 2, Name = "Zone 2", Pin = 27 },
            new() { Order = 3, Name = "Zone 3", Pin = 22 },
            new() { Order = 4, Name = "Zone 4", Pin = 5 }
        ];
    }
}
