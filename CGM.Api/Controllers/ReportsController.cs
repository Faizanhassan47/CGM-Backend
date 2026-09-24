using System.Security.Claims;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly CgmDbContext _db;

    public ReportsController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpGet("detailed")]
    public async Task<ActionResult<DetailedReportDto>> GetDetailedReport(
        [FromQuery] int? targetUserId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var effectiveUserId = targetUserId.HasValue && targetUserId.Value > 0 ? targetUserId.Value : currentUserId;

        // Authorization check if viewing another user's report
        if (effectiveUserId != currentUserId)
        {
            var isAuthorized = await _db.Families
                .AnyAsync(f => (f.OwnerUserId == currentUserId || f.Members.Any(m => m.UserId == currentUserId && m.Status == "Active")) &&
                               f.Members.Any(m => m.UserId == effectiveUserId && m.Status == "Active"), ct);

            if (!isAuthorized)
            {
                return Forbid("You do not have permission to view reports for this user.");
            }
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == effectiveUserId, ct);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var end = endDate?.Date ?? DateTime.UtcNow.Date;
        var start = startDate?.Date ?? end.AddDays(-6);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = _db.GlucoseMeasurements
            .Where(m => m.UserId == effectiveUserId && m.MeasurementTime >= startUtc && m.MeasurementTime <= endUtc)
            .OrderBy(m => m.MeasurementTime);

        var readings = await query.ToListAsync(ct);

        var report = new DetailedReportDto
        {
            UserId = effectiveUserId,
            PatientName = string.IsNullOrWhiteSpace(user.FullName) ? "GlucoTrack Patient" : user.FullName,
            PatientEmail = user.Email ?? string.Empty,
            StartDate = start,
            EndDate = end,
            DateRange = $"{start:MMM dd, yyyy} – {end:MMM dd, yyyy}",
            GeneratedAt = DateTime.UtcNow,
            TotalReadings = readings.Count
        };

        if (readings.Count == 0)
        {
            // If no readings in specified date range, fallback to latest available DB readings
            readings = await _db.GlucoseMeasurements
                .Where(m => m.UserId == effectiveUserId && m.GlucoseValue.HasValue)
                .OrderByDescending(m => m.MeasurementTime)
                .Take(40)
                .ToListAsync(ct);

            if (readings.Count > 0)
            {
                readings.Reverse();
                var earliest = readings.First().MeasurementTime;
                var latest = readings.Last().MeasurementTime;
                report.DateRange = $"{earliest:MMM dd, yyyy} – {latest:MMM dd, yyyy}";
                report.TotalReadings = readings.Count;
            }
            else
            {
                return Ok(report);
            }
        }

        var validValues = readings
            .Where(r => r.GlucoseValue.HasValue)
            .Select(r => r.GlucoseValue!.Value)
            .ToList();

        if (validValues.Count == 0)
        {
            return Ok(report);
        }

        var total = (decimal)validValues.Count;
        var avg = Math.Round(validValues.Average(), 0);
        var min = validValues.Min();
        var max = validValues.Max();

        var inRangeCount = validValues.Count(v => v is >= 70 and <= 180);
        var aboveRangeCount = validValues.Count(v => v > 180);
        var belowRangeCount = validValues.Count(v => v < 70);
        var veryHighCount = validValues.Count(v => v > 250);
        var veryLowCount = validValues.Count(v => v < 54);

        var tir = Math.Round((decimal)inRangeCount / total * 100, 1);
        var tar = Math.Round((decimal)aboveRangeCount / total * 100, 1);
        var tbr = Math.Round((decimal)belowRangeCount / total * 100, 1);
        var veryHigh = Math.Round((decimal)veryHighCount / total * 100, 1);
        var veryLow = Math.Round((decimal)veryLowCount / total * 100, 1);

        var a1c = Math.Round((avg + 46.7m) / 28.7m, 1);

        var stdDev = 0m;
        if (validValues.Count > 1)
        {
            var sumOfSquares = validValues.Sum(v => (v - avg) * (v - avg));
            stdDev = (decimal)Math.Sqrt((double)(sumOfSquares / (validValues.Count - 1)));
            stdDev = Math.Round(stdDev, 1);
        }

        var cv = avg > 0 ? Math.Round((stdDev / avg) * 100, 1) : 0;

        report.AvgGlucose = $"{avg} mg/dL";
        report.TimeInRange = $"{tir}%";
        report.TimeAboveRange = $"{tar}%";
        report.TimeBelowRange = $"{tbr}%";
        report.TimeVeryHigh = $"{veryHigh}%";
        report.TimeVeryLow = $"{veryLow}%";
        report.HighestGlucose = $"{max} mg/dL";
        report.LowestGlucose = $"{min} mg/dL";
        report.EstimatedA1c = $"{a1c}%";
        report.GlucoseVariability = $"{cv}%";
        report.StandardDeviation = $"{stdDev} mg/dL";

        report.TirPercentage = (double)tir;
        report.TarPercentage = (double)tar;
        report.TbrPercentage = (double)tbr;
        report.VeryHighPercentage = (double)veryHigh;
        report.VeryLowPercentage = (double)veryLow;

        // Group by Day for Daily breakdown
        var dailyGroups = readings
            .Where(r => r.GlucoseValue.HasValue)
            .GroupBy(r => r.MeasurementTime.Date)
            .OrderByDescending(g => g.Key);

        foreach (var group in dailyGroups)
        {
            var gValues = group.Select(r => r.GlucoseValue!.Value).ToList();
            var gAvg = Math.Round(gValues.Average(), 0);
            var gMin = gValues.Min();
            var gMax = gValues.Max();
            var gTir = Math.Round((decimal)gValues.Count(v => v is >= 70 and <= 180) / gValues.Count * 100, 0);

            var status = "Optimal";
            if (gTir < 70) status = "Variable";
            if (gMin < 70) status = "Low Detected";
            if (gMax > 250) status = "High Spike";

            report.DailySummaries.Add(new DailyReportBreakdownDto
            {
                DateFormatted = group.Key.ToString("ddd, MMM dd"),
                ReadingsCount = gValues.Count,
                AvgGlucose = $"{gAvg} mg/dL",
                MinGlucose = $"{gMin} mg/dL",
                MaxGlucose = $"{gMax} mg/dL",
                TimeInRange = $"{gTir}%",
                Status = status
            });
        }

        // Readings log (up to 40 records)
        var recentList = readings
            .OrderByDescending(r => r.MeasurementTime)
            .Take(40)
            .Select(r =>
            {
                var val = r.GlucoseValue ?? 0;
                var status = "Normal";
                if (val < 54) status = "Very Low";
                else if (val < 70) status = "Low";
                else if (val > 250) status = "Very High";
                else if (val > 180) status = "High";

                return new ReportReadingItemDto
                {
                    Time = r.MeasurementTime,
                    TimeFormatted = r.MeasurementTime.ToLocalTime().ToString("MMM dd, yyyy HH:mm"),
                    Value = val,
                    Status = status
                };
            })
            .ToList();

        report.RecentReadings = recentList;

        return Ok(report);
    }
}
