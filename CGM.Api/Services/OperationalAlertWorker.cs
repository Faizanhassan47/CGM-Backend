using CGM.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Services;

public sealed class OperationalAlertWorker(IServiceScopeFactory scopeFactory, ILogger<OperationalAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CgmDbContext>();
                var failedDeliveries = await db.AlertQueue.CountAsync(x => x.Status == "Failed", stoppingToken);
                var oldestPending = await db.AlertQueue.Where(x => x.Status == "Pending").MinAsync(x => (DateTime?)x.CreatedAt, stoppingToken);
                if (failedDeliveries >= 25) logger.LogError("Operations alert: {FailedDeliveries} failed notification queue items", failedDeliveries);
                if (oldestPending.HasValue && oldestPending < DateTime.UtcNow.AddMinutes(-10)) logger.LogError("Operations alert: notification queue lag exceeds ten minutes; oldest item {OldestPendingUtc}", oldestPending);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Operational monitor check failed"); }
        }
    }
}
