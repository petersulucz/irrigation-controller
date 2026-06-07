using Irrigation.Core.Configuration;
using Irrigation.Core.Model;
using Irrigation.Shared;
using Microsoft.EntityFrameworkCore;

namespace Irrigation.Core.Persistence;

public sealed class EfIrrigationStore(IDbContextFactory<IrrigationDbContext> dbFactory) : IIrrigationStore
{
    public async Task InitializeAsync(IReadOnlyList<ZoneOptions> configuredZones, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Zones.AnyAsync(cancellationToken))
        {
            foreach (var zone in configuredZones.OrderBy(z => z.Order))
            {
                db.Zones.Add(new ZoneRecord
                {
                    ZoneId = zone.ZoneId ?? CreateStableZoneId(zone.Order),
                    Order = zone.Order,
                    Name = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.Order}" : zone.Name,
                    Pin = zone.Pin,
                    Enabled = zone.Enabled,
                    DefaultDuration = zone.DefaultDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : zone.DefaultDuration
                });
            }
        }
        else
        {
            await SyncHardwareConfigAsync(db, configuredZones, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IrrigationZone>> GetZonesAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Zones
            .OrderBy(z => z.Order)
            .Select(z => new IrrigationZone(z.ZoneId, z.Order, z.Name, z.Pin, z.Enabled, z.DefaultDuration))
            .ToListAsync(cancellationToken);
    }

    public async Task<IrrigationZone?> UpdateZoneAsync(Guid zoneId, UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.ZoneId == zoneId, cancellationToken);
        if (zone is null)
        {
            return null;
        }

        zone.Name = string.IsNullOrWhiteSpace(request.Name) ? zone.Name : request.Name.Trim();
        zone.DefaultDuration = request.DefaultDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : request.DefaultDuration;
        zone.Enabled = request.Enabled;
        await db.SaveChangesAsync(cancellationToken);

        return new IrrigationZone(zone.ZoneId, zone.Order, zone.Name, zone.Pin, zone.Enabled, zone.DefaultDuration);
    }

    public async Task RecordTelemetryAsync(string eventType, string message, Guid? zoneId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.TelemetryEvents.Add(new TelemetryEventRecord
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            Message = message,
            ZoneId = zoneId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> BeginRunAsync(IrrigationZone zone, TimeSpan plannedDuration, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runId = Guid.NewGuid();
        db.RunHistory.Add(new RunHistoryRecord
        {
            RunId = runId,
            ZoneId = zone.ZoneId,
            ZoneName = zone.Name,
            StartedAt = DateTimeOffset.UtcNow,
            PlannedDuration = plannedDuration,
            Outcome = "Running"
        });
        await db.SaveChangesAsync(cancellationToken);
        return runId;
    }

    public async Task CompleteRunAsync(Guid runId, string outcome, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.RunHistory.FirstOrDefaultAsync(r => r.RunId == runId, cancellationToken);
        if (run is null || run.CompletedAt is not null)
        {
            return;
        }

        var completedAt = DateTimeOffset.UtcNow;
        run.CompletedAt = completedAt;
        run.ActualDuration = completedAt - run.StartedAt;
        run.Outcome = outcome;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RunHistoryDto>> GetRunHistoryAsync(int count, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.RunHistory.ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 500))
            .Select(ToDto)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, RunHistoryDto>> GetLastRunsAsync(CancellationToken cancellationToken)
    {
        var history = await this.GetRunHistoryAsync(500, cancellationToken);
        return history
            .GroupBy(r => r.ZoneId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.StartedAt).First());
    }

    public async Task<IReadOnlyList<TelemetryEventDto>> GetTelemetryAsync(int count, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.TelemetryEvents.ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Clamp(count, 1, 500))
            .Select(e => new TelemetryEventDto(e.Timestamp, e.EventType, e.Message, e.ZoneId))
            .ToList();
    }

    private static async Task SyncHardwareConfigAsync(IrrigationDbContext db, IReadOnlyList<ZoneOptions> configuredZones, CancellationToken cancellationToken)
    {
        foreach (var configured in configuredZones)
        {
            var zoneId = configured.ZoneId ?? CreateStableZoneId(configured.Order);
            var existing = await db.Zones.FirstOrDefaultAsync(z => z.ZoneId == zoneId, cancellationToken);
            if (existing is null)
            {
                db.Zones.Add(new ZoneRecord
                {
                    ZoneId = zoneId,
                    Order = configured.Order,
                    Name = string.IsNullOrWhiteSpace(configured.Name) ? $"Zone {configured.Order}" : configured.Name,
                    Pin = configured.Pin,
                    Enabled = configured.Enabled,
                    DefaultDuration = configured.DefaultDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : configured.DefaultDuration
                });
            }
            else
            {
                existing.Order = configured.Order;
                existing.Pin = configured.Pin;
            }
        }
    }

    private static RunHistoryDto ToDto(RunHistoryRecord r)
    {
        return new RunHistoryDto(r.RunId, r.ZoneId, r.ZoneName, r.StartedAt, r.CompletedAt, r.PlannedDuration, r.ActualDuration, r.Outcome);
    }

    private static Guid CreateStableZoneId(int order)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..4], order);
        bytes[15] = 1;
        return new Guid(bytes);
    }
}
