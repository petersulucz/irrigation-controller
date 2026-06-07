using Irrigation.Core.Configuration;
using Irrigation.Core.Hardware;
using Irrigation.Core.Persistence;
using Irrigation.Core.Runtime;
using Irrigation.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Irrigation.Tests;

public sealed class IrrigationRuntimeTests
{
    [Fact]
    public async Task RunZone_ReplacesActiveZoneImmediately()
    {
        await using var fixture = await RuntimeFixture.CreateAsync();
        var zones = (await fixture.Runtime.GetStatusAsync(CancellationToken.None)).Zones;

        await fixture.Runtime.RunZoneAsync(zones[0].ZoneId, CancellationToken.None);
        await fixture.Runtime.RunZoneAsync(zones[1].ZoneId, CancellationToken.None);

        var status = await fixture.Runtime.GetStatusAsync(CancellationToken.None);
        Assert.Equal(zones[1].ZoneId, status.ActiveZoneId);
        Assert.Equal(ZoneVisualState.Idle, status.Zones.Single(z => z.ZoneId == zones[0].ZoneId).State);
        Assert.Equal(ZoneVisualState.Running, status.Zones.Single(z => z.ZoneId == zones[1].ZoneId).State);
        Assert.False(status.SimulatedPins[4]);
        Assert.True(status.SimulatedPins[27]);
    }

    [Fact]
    public async Task RunAll_QueuesAllEnabledZonesAfterFirstZone()
    {
        await using var fixture = await RuntimeFixture.CreateAsync();

        await fixture.Runtime.RunAllZonesAsync(CancellationToken.None);

        var status = await fixture.Runtime.GetStatusAsync(CancellationToken.None);
        Assert.Equal("Front Lawn", status.Zones.Single(z => z.State == ZoneVisualState.Running).Name);
        Assert.Equal(2, status.Queue.Count);
        Assert.Equal("Back Lawn", status.Queue[0].Name);
        Assert.Equal("Garden Beds", status.Queue[1].Name);
    }

    [Fact]
    public async Task StopAll_ClearsActiveZoneAndQueue()
    {
        await using var fixture = await RuntimeFixture.CreateAsync();

        await fixture.Runtime.RunAllZonesAsync(CancellationToken.None);
        await fixture.Runtime.StopAllAsync(CancellationToken.None);

        var status = await fixture.Runtime.GetStatusAsync(CancellationToken.None);
        Assert.Null(status.ActiveZoneId);
        Assert.Empty(status.Queue);
        Assert.All(status.Zones.Where(z => z.Enabled), z => Assert.Equal(ZoneVisualState.Idle, z.State));
        Assert.All(status.SimulatedPins.Values, Assert.False);
    }

    [Fact]
    public async Task UpdateZone_PersistsNameAndDuration()
    {
        await using var fixture = await RuntimeFixture.CreateAsync();
        var zone = (await fixture.Runtime.GetStatusAsync(CancellationToken.None)).Zones[0];

        await fixture.Runtime.UpdateZoneAsync(zone.ZoneId, new UpdateZoneRequest("Driveway Strip", TimeSpan.FromMinutes(7), true), CancellationToken.None);

        var status = await fixture.Runtime.GetStatusAsync(CancellationToken.None);
        var updated = status.Zones.Single(z => z.ZoneId == zone.ZoneId);
        Assert.Equal("Driveway Strip", updated.Name);
        Assert.Equal(TimeSpan.FromMinutes(7), updated.DefaultDuration);
    }

    [Fact]
    public async Task RunHistory_IsRecordedWhenZoneIsStopped()
    {
        await using var fixture = await RuntimeFixture.CreateAsync();
        var zone = (await fixture.Runtime.GetStatusAsync(CancellationToken.None)).Zones[0];

        await fixture.Runtime.RunZoneAsync(zone.ZoneId, CancellationToken.None);
        await fixture.Runtime.StopAllAsync(CancellationToken.None);

        var history = await fixture.Store.GetRunHistoryAsync(10, CancellationToken.None);
        Assert.Single(history);
        Assert.Equal(zone.ZoneId, history[0].ZoneId);
        Assert.Equal("Stopped", history[0].Outcome);
        Assert.NotNull(history[0].CompletedAt);
    }

    private sealed class RuntimeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private RuntimeFixture(IrrigationRuntime runtime, EfIrrigationStore store, SqliteConnection connection)
        {
            this.Runtime = runtime;
            this.Store = store;
            this.connection = connection;
        }

        public IrrigationRuntime Runtime { get; }

        public EfIrrigationStore Store { get; }

        public static async Task<RuntimeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<IrrigationDbContext>()
                .UseSqlite(connection)
                .Options;
            var factory = new TestDbContextFactory(options);
            var store = new EfIrrigationStore(factory);
            var runtime = new IrrigationRuntime(
                Options.Create(new IrrigationOptions
                {
                    SetupComplete = true,
                    Hardware = new HardwareOptions
                    {
                        Provider = "Simulated",
                        Zones =
                        [
                            new() { Order = 1, Name = "Front Lawn", Pin = 4, Enabled = true, DefaultDuration = TimeSpan.FromMinutes(5) },
                            new() { Order = 2, Name = "Back Lawn", Pin = 27, Enabled = true, DefaultDuration = TimeSpan.FromMinutes(5) },
                            new() { Order = 3, Name = "Garden Beds", Pin = 22, Enabled = true, DefaultDuration = TimeSpan.FromMinutes(5) },
                            new() { Order = 4, Name = "Disabled", Pin = 5, Enabled = false, DefaultDuration = TimeSpan.FromMinutes(5) }
                        ]
                    }
                }),
                new SimulatedRelayController(),
                store);

            await runtime.InitializeAsync(CancellationToken.None);
            return new RuntimeFixture(runtime, store, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Runtime.DisposeAsync();
            await this.connection.DisposeAsync();
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<IrrigationDbContext> options) : IDbContextFactory<IrrigationDbContext>
    {
        public IrrigationDbContext CreateDbContext() => new(options);
    }
}
