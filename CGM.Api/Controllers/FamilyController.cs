using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using CGM.Api.Services;

namespace CGM.Api.Controllers;

[ApiController, Authorize, Route("api/family")]
public class FamilyController(CgmDbContext db, IReferralCodeService referralCodes) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");

    [HttpGet]
    public async Task<ActionResult<FamilyResponse>> Get(CancellationToken ct)
    {
        await EnsureReferralCode(UserId, ct);
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        return family is null ? NotFound(new { message = "You do not belong to an active family." }) : Ok(ToResponse(family, UserId));
    }

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<FamilyMemberResponse>>> Members(CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        return family is null ? NotFound(new { message = "You do not belong to an active family." }) : Ok(ToResponse(family, UserId).Members);
    }

    [HttpPost("create")]
    public async Task<ActionResult<FamilyResponse>> Create(CreateFamilyRequest request, CancellationToken ct)
    {
        var name = request.FamilyName.Trim();
        if (name.Length == 0) return BadRequest(new { message = "Family name is required." });
        var existing = await db.Families.Include(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(f => f.OwnerUserId == UserId && f.IsActive, ct);
        if (existing is not null) return Ok(ToResponse(existing, UserId));
        if (await db.FamilyMembers.AnyAsync(m => m.UserId == UserId && m.Status == "Active" && m.Family.IsActive, ct))
            return Conflict(new { message = "You already belong to an active family." });
        var user = await db.Users.SingleAsync(u => u.Id == UserId && u.IsActive, ct);
        if (string.IsNullOrEmpty(user.ReferralCode)) { user.ReferralCode = await referralCodes.GenerateUniqueAsync(ct); await db.SaveChangesAsync(ct); }
        var family = new FamilyEntity { FamilyName = name, OwnerUserId = user.Id };
        family.Members.Add(new FamilyMemberEntity { UserId = user.Id, User = user, MemberEmail = user.Email, JoinedByReferralCode = user.ReferralCode, Role = "Owner" });
        db.Families.Add(family);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpPost("join")]
    public async Task<ActionResult<FamilyResponse>> Join(JoinFamilyRequest request, CancellationToken ct)
    {
        var code = request.ReferralCode.Trim().ToUpperInvariant();
        if (code.Length == 0) return BadRequest(new { message = "Referral code is required." });
        var current = await db.Users.SingleAsync(u => u.Id == UserId && u.IsActive, ct);
        var owner = await db.Users.FirstOrDefaultAsync(u => u.ReferralCode == code && u.IsActive, ct);
        if (owner is null) return BadRequest(new { message = "Referral code is invalid." });
        if (owner.Id == current.Id) return BadRequest(new { message = "You cannot join a family using your own referral code." });
        if (await db.FamilyMembers.AnyAsync(m => m.UserId == current.Id && m.Status == "Active" && m.Family.IsActive, ct))
            return Conflict(new { message = "You already belong to an active family." });
        var family = await db.Families.Include(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(f => f.OwnerUserId == owner.Id && f.IsActive, ct);
        if (family is null)
        {
            family = new FamilyEntity { FamilyName = $"{owner.FullName}'s Family", OwnerUserId = owner.Id };
            family.Members.Add(new FamilyMemberEntity { UserId = owner.Id, User = owner, MemberEmail = owner.Email, JoinedByReferralCode = owner.ReferralCode, Role = "Owner" });
            db.Families.Add(family);
        }
        var membership = family.Members.FirstOrDefault(m => m.UserId == current.Id);
        if (membership?.Status == "Active") return Conflict(new { message = "You are already a member of this family." });
        if (membership is null)
            family.Members.Add(new FamilyMemberEntity { UserId = current.Id, User = current, MemberEmail = current.Email, JoinedByReferralCode = code });
        else { membership.Status = "Active"; membership.ReceiveAlerts = true; membership.JoinedAt = DateTime.UtcNow; membership.JoinedByReferralCode = code; }
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpPut("members/{targetUserId:int}/alerts")]
    public async Task<IActionResult> Alerts(int targetUserId, UpdateFamilyAlertPreferenceRequest request, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId != UserId && targetUserId != UserId) return Forbid();
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound();
        member.ReceiveAlerts = request.ReceiveAlerts;
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Alert preference updated." });
    }

    [HttpPut("members/{targetUserId:int}/role")]
    public async Task<IActionResult> Role(int targetUserId, UpdateCareCircleRoleRequest request, CancellationToken ct)
    {
        var roles = new[] { "Parent", "Doctor", "Caregiver", "EmergencyContact" };
        if (!roles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Role must be Parent, Doctor, Caregiver, or EmergencyContact." });
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId != UserId) return Forbid();
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound();
        if (targetUserId == family.OwnerUserId) return BadRequest(new { message = "The owner role cannot be changed." });
        member.Role = roles.First(r => r.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Care-circle role updated.", role = member.Role });
    }

    [HttpPut("thresholds")]
    public async Task<IActionResult> Thresholds(UpdateFamilyThresholdsRequest request, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound(new { message = "You do not belong to an active family." });
        if (family.OwnerUserId != UserId) return Forbid();
        if (request.LowGlucoseThreshold < 40 || request.LowGlucoseThreshold > 120)
            return BadRequest(new { message = "Minimum glucose must be between 40 and 120 mg/dL." });
        if (request.HighGlucoseThreshold < 121 || request.HighGlucoseThreshold > 400)
            return BadRequest(new { message = "Maximum glucose must be between 121 and 400 mg/dL." });
        if (request.LowGlucoseThreshold >= request.HighGlucoseThreshold)
            return BadRequest(new { message = "Minimum glucose must be lower than maximum glucose." });
        family.LowGlucoseThreshold = request.LowGlucoseThreshold;
        family.HighGlucoseThreshold = request.HighGlucoseThreshold;
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpDelete("members/{targetUserId:int}")]
    public async Task<IActionResult> Remove(int targetUserId, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId != UserId) return Forbid();
        if (targetUserId == UserId) return BadRequest(new { message = "The owner cannot be removed." });
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound();
        member.Status = "Removed";
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Family member removed." });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId == UserId && family.Members.Any(m => m.UserId != UserId && m.Status == "Active"))
            return Conflict(new { message = "Owner cannot leave while active family members remain." });
        var member = family.Members.Single(m => m.UserId == UserId && m.Status == "Active");
        member.Status = "Left";
        if (family.OwnerUserId == UserId) family.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "You left the family." });
    }

    [HttpPost("notify")]
    public async Task<IActionResult> Notify(SendNotificationRequest request, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound(new { message = "You do not belong to an active family." });
        if (family.OwnerUserId != UserId) return Forbid();

        var targetMembers = family.Members.Where(m => m.Status == "Active" && m.UserId != UserId);
        if (request.Target == "AlertsOn")
        {
            targetMembers = targetMembers.Where(m => m.ReceiveAlerts);
        }

        foreach (var member in targetMembers)
        {
            if (member.UserId.HasValue)
            {
                db.Alerts.Add(new AlertEntity
                {
                    UserId = member.UserId.Value,
                    AlertType = "Custom",
                    Title = "Family Notification",
                    Message = request.Message,
                    Severity = "Info"
                });
            }
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Notifications sent." });
    }

    [HttpGet("weekly-report")]
    public async Task<ActionResult<IReadOnlyList<WeeklyReportItemResponse>>> WeeklyReport(CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound(new { message = "You do not belong to an active family." });
        if (family.OwnerUserId != UserId) return Forbid();

        var memberUserIds = family.Members.Where(m => m.Status == "Active" && m.UserId.HasValue).Select(m => m.UserId.Value).ToList();
        
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var alerts = await db.Alerts
            .Include(a => a.User)
            .Where(a => memberUserIds.Contains(a.UserId) && a.AlertTime >= sevenDaysAgo)
            .OrderByDescending(a => a.AlertTime)
            .Select(a => new WeeklyReportItemResponse(a.UserId, a.User.FullName, a.AlertType, a.GlucoseValue, a.AlertTime))
            .ToListAsync(ct);

        return Ok(alerts);
    }

    [HttpGet("members/{targetUserId:int}/report-summary")]
    public async Task<ActionResult> MemberReportSummary(int targetUserId, [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound(new { message = "You do not belong to an active family." });
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound(new { message = "Member not found." });
        
        var startDate = start.Date;
        var endDate = end.Date;
        if (startDate > endDate) (startDate, endDate) = (endDate, startDate);

        var startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = db.GlucoseMeasurements
            .Where(m => m.UserId == targetUserId && m.MeasurementTime >= startUtc && m.MeasurementTime <= endUtc)
            .OrderBy(m => m.MeasurementTime);

        var readings = await query.ToListAsync(ct);
        
        var patientName = member.User?.FullName ?? "Family Member";
        var patientEmail = member.User?.Email ?? "";
        var dateRange = $"{startDate:MMM dd, yyyy} – {endDate:MMM dd, yyyy}";

        if (readings.Count == 0)
        {
            readings = await db.GlucoseMeasurements
                .Where(m => m.UserId == targetUserId && m.GlucoseValue.HasValue)
                .OrderByDescending(m => m.MeasurementTime)
                .Take(40)
                .ToListAsync(ct);

            if (readings.Count > 0)
            {
                readings.Reverse();
                var earliest = readings.First().MeasurementTime;
                var latest = readings.Last().MeasurementTime;
                dateRange = $"{earliest:MMM dd, yyyy} – {latest:MMM dd, yyyy}";
            }
        }

        var validReadings = readings.Where(r => r.GlucoseValue.HasValue).Select(r => r.GlucoseValue!.Value).ToList();
        if (validReadings.Count == 0)
        {
            return Ok(new DetailedReportDto
            {
                UserId = targetUserId,
                PatientName = patientName,
                PatientEmail = patientEmail,
                DateRange = dateRange,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow,
                TotalReadings = 0
            });
        }

        var total = (decimal)validReadings.Count;
        var avg = Math.Round(validReadings.Average(), 0);
        var min = validReadings.Min();
        var max = validReadings.Max();

        var inRangeCount = validReadings.Count(v => v is >= 70 and <= 180);
        var aboveRangeCount = validReadings.Count(v => v > 180);
        var belowRangeCount = validReadings.Count(v => v < 70);
        var veryHighCount = validReadings.Count(v => v > 250);
        var veryLowCount = validReadings.Count(v => v < 54);

        var tir = Math.Round((decimal)inRangeCount / total * 100, 1);
        var tar = Math.Round((decimal)aboveRangeCount / total * 100, 1);
        var tbr = Math.Round((decimal)belowRangeCount / total * 100, 1);
        var veryHigh = Math.Round((decimal)veryHighCount / total * 100, 1);
        var veryLow = Math.Round((decimal)veryLowCount / total * 100, 1);

        var a1c = Math.Round((avg + 46.7m) / 28.7m, 1);
        var stdDev = 0m;
        if (validReadings.Count > 1)
        {
            var sumSq = validReadings.Sum(v => (v - avg) * (v - avg));
            stdDev = Math.Round((decimal)Math.Sqrt((double)(sumSq / (validReadings.Count - 1))), 1);
        }
        var cv = avg > 0 ? Math.Round((stdDev / avg) * 100, 1) : 0;

        var dailySummaries = readings
            .Where(r => r.GlucoseValue.HasValue)
            .GroupBy(r => r.MeasurementTime.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var gVals = g.Select(r => r.GlucoseValue!.Value).ToList();
                var gAvg = Math.Round(gVals.Average(), 0);
                var gMin = gVals.Min();
                var gMax = gVals.Max();
                var gTir = Math.Round((decimal)gVals.Count(v => v is >= 70 and <= 180) / gVals.Count * 100, 0);
                return new DailyReportBreakdownDto
                {
                    DateFormatted = g.Key.ToString("ddd, MMM dd"),
                    ReadingsCount = gVals.Count,
                    AvgGlucose = $"{gAvg} mg/dL",
                    MinGlucose = $"{gMin} mg/dL",
                    MaxGlucose = $"{gMax} mg/dL",
                    TimeInRange = $"{gTir}%",
                    Status = gTir >= 70 ? "Optimal" : (gMin < 70 ? "Low Detected" : "Variable")
                };
            })
            .ToList();

        var recentList = readings
            .OrderByDescending(r => r.MeasurementTime)
            .Take(40)
            .Select(r => new ReportReadingItemDto
            {
                Time = r.MeasurementTime,
                TimeFormatted = r.MeasurementTime.ToLocalTime().ToString("MMM dd, yyyy HH:mm"),
                Value = r.GlucoseValue ?? 0,
                Status = (r.GlucoseValue ?? 0) < 70 ? "Low" : ((r.GlucoseValue ?? 0) > 180 ? "High" : "Normal")
            })
            .ToList();

        return Ok(new DetailedReportDto
        {
            UserId = targetUserId,
            PatientName = patientName,
            PatientEmail = patientEmail,
            DateRange = dateRange,
            StartDate = startDate,
            EndDate = endDate,
            GeneratedAt = DateTime.UtcNow,
            TotalReadings = validReadings.Count,
            AvgGlucose = $"{avg} mg/dL",
            TimeInRange = $"{tir}%",
            TimeAboveRange = $"{tar}%",
            TimeBelowRange = $"{tbr}%",
            TimeVeryHigh = $"{veryHigh}%",
            TimeVeryLow = $"{veryLow}%",
            HighestGlucose = $"{max} mg/dL",
            LowestGlucose = $"{min} mg/dL",
            EstimatedA1c = $"{a1c}%",
            GlucoseVariability = $"{cv}%",
            StandardDeviation = $"{stdDev} mg/dL",
            TirPercentage = (double)tir,
            TarPercentage = (double)tar,
            TbrPercentage = (double)tbr,
            VeryHighPercentage = (double)veryHigh,
            VeryLowPercentage = (double)veryLow,
            DailySummaries = dailySummaries,
            RecentReadings = recentList
        });
    }

    private IQueryable<FamilyEntity> VisibleFamily(int userId) => db.Families
        .Include(f => f.Members).ThenInclude(m => m.User)
        .Where(f => f.IsActive && (f.OwnerUserId == userId || f.Members.Any(m => m.UserId == userId && m.Status == "Active")));

    private static FamilyResponse ToResponse(FamilyEntity f, int currentUserId) => new(f.Id, f.FamilyName, f.OwnerUserId, currentUserId,
        f.OwnerUserId == currentUserId,
        f.Members.FirstOrDefault(m => m.UserId == currentUserId)?.User?.ReferralCode ?? string.Empty,
        f.LowGlucoseThreshold, f.HighGlucoseThreshold,
        f.Members.Where(m => m.Status == "Active" && m.User != null).Select(m =>
            new FamilyMemberResponse(m.UserId!.Value, m.User!.FullName, m.MemberEmail, m.Role, m.ReceiveAlerts, m.Status, m.JoinedAt)).ToList());

    private async Task EnsureReferralCode(int userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (!string.IsNullOrEmpty(user.ReferralCode)) return;
        user.ReferralCode = await referralCodes.GenerateUniqueAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}
