using Microsoft.EntityFrameworkCore;

namespace Irrigation.Core.Persistence;

public sealed class IrrigationDbContext(DbContextOptions<IrrigationDbContext> options) : DbContext(options)
{
    public DbSet<ZoneRecord> Zones => this.Set<ZoneRecord>();

    public DbSet<TelemetryEventRecord> TelemetryEvents => this.Set<TelemetryEventRecord>();

    public DbSet<RunHistoryRecord> RunHistory => this.Set<RunHistoryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ZoneRecord>().HasKey(z => z.ZoneId);
        modelBuilder.Entity<ZoneRecord>().HasIndex(z => z.Order).IsUnique();
        modelBuilder.Entity<TelemetryEventRecord>().HasKey(e => e.EventId);
        modelBuilder.Entity<TelemetryEventRecord>().HasIndex(e => e.Timestamp);
        modelBuilder.Entity<RunHistoryRecord>().HasKey(r => r.RunId);
        modelBuilder.Entity<RunHistoryRecord>().HasIndex(r => r.StartedAt);
    }
}

public sealed class ZoneRecord
{
    public Guid ZoneId { get; set; }

    public int Order { get; set; }

    public required string Name { get; set; }

    public int Pin { get; set; }

    public bool Enabled { get; set; }

    public TimeSpan DefaultDuration { get; set; }
}

public sealed class TelemetryEventRecord
{
    public Guid EventId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public required string EventType { get; set; }

    public required string Message { get; set; }

    public Guid? ZoneId { get; set; }
}

public sealed class RunHistoryRecord
{
    public Guid RunId { get; set; }

    public Guid ZoneId { get; set; }

    public required string ZoneName { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public TimeSpan PlannedDuration { get; set; }

    public TimeSpan? ActualDuration { get; set; }

    public required string Outcome { get; set; }
}
