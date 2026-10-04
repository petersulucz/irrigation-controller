using Irrigation.Core.Persistence;

internal sealed class IrrigationMaintenanceService(
    EfIrrigationStore store,
    ILogger<IrrigationMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await store.MaintainAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Storage faults must stop watering and trigger service recovery.
                logger.LogError(ex, "Irrigation data maintenance failed");
                throw;
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
