using CGM.Api.Data;
using CGM.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Services;

public interface IGlucoseAlertService
{
    Task CreateIfAbnormalAsync(GlucoseMeasurementEntity measurement, CancellationToken cancellationToken = default);
}

public sealed class GlucoseAlertService(CgmDbContext db) : IGlucoseAlertService
{
    public async Task CreateIfAbnormalAsync(GlucoseMeasurementEntity measurement, CancellationToken cancellationToken = default)
    {
        if (!measurement.GlucoseValue.HasValue) return;
        var value = measurement.GlucoseValue.Value;
        var rules = await db.AlertRules.Where(r => r.UserId == measurement.UserId && r.Enabled).ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            rules =
            [
                new AlertRuleEntity { UserId=measurement.UserId, AlertType="LowGlucose", MaximumValue=70, DurationMinutes=15 },
                new AlertRuleEntity { UserId=measurement.UserId, AlertType="HighGlucose", MinimumValue=180, DurationMinutes=30 }
            ];
        }

        var rule = rules.FirstOrDefault(r =>
            (!r.MinimumValue.HasValue || value >= r.MinimumValue) &&
            (!r.MaximumValue.HasValue || value <= r.MaximumValue));
        if (rule is null) return;

        var duplicateSince = measurement.MeasurementTime.AddMinutes(-Math.Max(1, rule.DurationMinutes));
        if (await db.Alerts.AnyAsync(a => a.UserId == measurement.UserId && a.AlertType == rule.AlertType
            && a.AlertTime >= duplicateSince, cancellationToken)) return;

        var patient = await db.Users.SingleAsync(u => u.Id == measurement.UserId, cancellationToken);
        var isLow = rule.AlertType.Contains("Low", StringComparison.OrdinalIgnoreCase);
        var threshold = isLow ? rule.MaximumValue : rule.MinimumValue;
        var alert = new AlertEntity
        {
            UserId = patient.Id, SensorId = measurement.SensorId,
            AlertType = rule.AlertType, Title = isLow ? "Low Glucose Alert" : "High Glucose Alert",
            Message = $"{patient.FullName}'s glucose is {value:0.##} mg/dL.",
            GlucoseValue = value, GlucoseUnit = "mg/dL",
            Severity = isLow || value >= 250 ? "Critical" : "Warning", AlertTime = measurement.MeasurementTime
        };

        var familyId = await db.FamilyMembers.Where(m => m.UserId == patient.Id && m.Status == "Active" && m.Family.IsActive)
            .Select(m => (int?)m.FamilyId).FirstOrDefaultAsync(cancellationToken)
            ?? await db.Families.Where(f => f.OwnerUserId == patient.Id && f.IsActive).Select(f => (int?)f.Id).FirstOrDefaultAsync(cancellationToken);
        var recipientIds = familyId.HasValue
            ? await db.FamilyMembers.Where(m => m.FamilyId == familyId && m.Status == "Active" && m.ReceiveAlerts && m.UserId != null
                && (m.Role != "EmergencyContact" || alert.Severity == "Critical"))
                .Select(m => m.UserId!.Value).Distinct().ToListAsync(cancellationToken) : [];
        if (!recipientIds.Contains(patient.Id)) recipientIds.Add(patient.Id);
        foreach (var id in recipientIds) alert.Recipients.Add(new AlertRecipientEntity { UserId = id });

        db.Alerts.Add(alert);
        db.AlertHistory.Add(new AlertHistoryEntity { Alert=alert, UserId=patient.Id, AlertType=alert.AlertType,
            Threshold=threshold, TriggeredAt=measurement.MeasurementTime, Status="Queued" });
        db.AlertQueue.Add(new AlertQueueEntity { Alert=alert, Status="Pending", NextAttemptAt=DateTime.UtcNow });
    }
}
