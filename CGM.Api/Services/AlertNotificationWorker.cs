using CGM.Api.Data;
using CGM.Api.Models.Entities;
using CGM.Api.Services.Email;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CGM.Api.Services;

public sealed class AlertNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<AlertNotificationWorker> logger, OperationalMetrics metrics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { metrics.RecordWorkerHeartbeat(); await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { metrics.RecordNotificationFailure(); logger.LogError(ex, "Alert notification batch failed"); }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CgmDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var external = scope.ServiceProvider.GetRequiredService<IExternalNotificationSender>();
        List<AlertQueueEntity> items = [];
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var claim = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            items = await db.AlertQueue.Include(q => q.Alert).ThenInclude(a => a.Recipients)
                .Include(q => q.Alert).ThenInclude(a => a.User)
                .Where(q => (q.Status == "Pending" || q.Status == "Failed") && q.RetryCount < 3
                    && (!q.NextAttemptAt.HasValue || q.NextAttemptAt <= DateTime.UtcNow))
                .OrderBy(q => q.CreatedAt).Take(25).ToListAsync(ct);
            foreach (var item in items) item.Status = "Processing";
            await db.SaveChangesAsync(ct);
            await claim.CommitAsync(ct);
        });

        foreach (var item in items)
        {
            var users = await db.Users.Where(u => item.Alert.Recipients.Select(r => r.UserId).Contains(u.Id) && u.IsActive).ToListAsync(ct);
            var deliveredKeys = (await db.AlertDeliveryHistory
                .Where(x => x.AlertId == item.AlertId && x.Status == "Delivered")
                .Select(x => x.Channel + "|" + x.Destination).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var history = await db.AlertHistory.FirstOrDefaultAsync(h => h.AlertId == item.AlertId, ct);
            var threshold = history?.Threshold ?? item.Alert.GlucoseValue ?? 0;
            var allDelivered = true;
            foreach (var user in users)
            {
                if (!deliveredKeys.Contains($"Email|{user.Email}"))
                {
                    var sent = await email.SendGlucoseAlertEmailAsync(user.Email, user.FullName, item.Alert.User.FullName,
                        item.Alert.GlucoseValue ?? 0, item.Alert.GlucoseUnit ?? "mg/dL", threshold,
                        item.Alert.AlertType.Contains("Low", StringComparison.OrdinalIgnoreCase));
                    db.AlertDeliveryHistory.Add(new AlertDeliveryHistoryEntity { AlertId=item.AlertId, Channel="Email",
                        Destination=user.Email, SentAt=DateTime.UtcNow, DeliveredAt=sent ? DateTime.UtcNow : null,
                        Status=sent ? "Delivered" : "Failed", FailureReason=sent ? null : "Email provider rejected delivery" });
                    allDelivered &= sent;
                }
                var endpoints = await db.NotificationEndpoints.Where(x => x.UserId == user.Id && x.Enabled).ToListAsync(ct);
                foreach (var endpoint in endpoints)
                {
                    if (deliveredKeys.Contains($"{endpoint.Channel}|{endpoint.Address}")) continue;
                    var externalSent = await external.SendAsync(endpoint.Channel, endpoint.Address, item.Alert, ct);
                    db.AlertDeliveryHistory.Add(new AlertDeliveryHistoryEntity { AlertId=item.AlertId, Channel=endpoint.Channel,
                        Destination=endpoint.Address, SentAt=DateTime.UtcNow, DeliveredAt=externalSent ? DateTime.UtcNow : null,
                        Status=externalSent ? "Delivered" : "Failed", FailureReason=externalSent ? null : "Provider unavailable or rejected delivery" });
                    allDelivered &= externalSent;
                }
            }
            item.RetryCount++;
            item.Status = allDelivered ? "Completed" : item.RetryCount >= 3 ? "Failed" : "Pending";
            item.ProcessedAt = allDelivered ? DateTime.UtcNow : null;
            item.NextAttemptAt = allDelivered ? null : DateTime.UtcNow.AddMinutes(Math.Pow(2, item.RetryCount));
            item.ErrorMessage = allDelivered ? null : "One or more notification deliveries failed.";
            if (!allDelivered) metrics.RecordNotificationFailure();
            if (history is not null) { history.Status=item.Status; history.DeliveredAt=allDelivered ? DateTime.UtcNow : null; }
            await db.SaveChangesAsync(ct);
        }
    }
}
