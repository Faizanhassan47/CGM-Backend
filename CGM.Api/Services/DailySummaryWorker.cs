using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Entities;

namespace CGM.Api.Services;

public class DailySummaryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailySummaryWorker> _logger;

    public DailySummaryWorker(IServiceScopeFactory scopeFactory, ILogger<DailySummaryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailySummaryWorker started. Will aggregate nightly at 00:05 UTC.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddMinutes(5); // Next 00:05 UTC
            var delay = nextRun - now;

            try
            {
                await Task.Delay(delay, stoppingToken);
                var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                await ProcessDailyAggregationAsync(yesterday, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during nightly daily summary execution.");
                // Prevent tight loop on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    public async Task ProcessDailyAggregationAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CgmDbContext>();

        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var activeUserIds = await db.GlucoseMeasurements
            .Where(m => m.MeasurementTime >= startUtc && m.MeasurementTime <= endUtc)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Aggregating daily summaries for {Count} users for date {Date}...", activeUserIds.Count, date);

        foreach (var userId in activeUserIds)
        {
            var readings = await db.GlucoseMeasurements
                .Where(m => m.UserId == userId && m.MeasurementTime >= startUtc && m.MeasurementTime <= endUtc && m.GlucoseValue != null)
                .OrderBy(m => m.MeasurementTime)
                .Select(m => m.GlucoseValue!.Value)
                .ToListAsync(cancellationToken);

            if (readings.Count == 0) continue;

            var summary = CalculateSummary(userId, date, readings);

            var existing = await db.DailyGlucoseSummaries
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SummaryDate == date, cancellationToken);

            if (existing != null)
            {
                existing.ReadingCount = summary.ReadingCount;
                existing.MeanGlucose = summary.MeanGlucose;
                existing.MedianGlucose = summary.MedianGlucose;
                existing.StandardDeviation = summary.StandardDeviation;
                existing.Gmi = summary.Gmi;
                existing.CvPercentage = summary.CvPercentage;
                existing.TimeInRangeMinutes = summary.TimeInRangeMinutes;
                existing.TimeBelowRangeMinutes = summary.TimeBelowRangeMinutes;
                existing.TimeAboveRangeMinutes = summary.TimeAboveRangeMinutes;
                existing.TimeInRangePercentage = summary.TimeInRangePercentage;
                existing.TimeBelowRangePercentage = summary.TimeBelowRangePercentage;
                existing.TimeAboveRangePercentage = summary.TimeAboveRangePercentage;
                existing.LowestGlucose = summary.LowestGlucose;
                existing.HighestGlucose = summary.HighestGlucose;
                existing.HighEventsCount = summary.HighEventsCount;
                existing.LowEventsCount = summary.LowEventsCount;
                existing.GeneratedAt = DateTime.UtcNow;
            }
            else
            {
                db.DailyGlucoseSummaries.Add(summary);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Daily summary aggregation completed successfully for date {Date}.", date);
    }

    public static DailyGlucoseSummaryEntity CalculateSummary(int userId, DateOnly date, IReadOnlyList<decimal> readings)
    {
        if (readings.Count == 0)
        {
            return new DailyGlucoseSummaryEntity
            {
                UserId = userId,
                SummaryDate = date,
                ReadingCount = 0
            };
        }

        var count = readings.Count;
        var mean = readings.Average();
        
        var sorted = readings.OrderBy(x => x).ToList();
        decimal median = count % 2 == 1
            ? sorted[count / 2]
            : (sorted[(count / 2) - 1] + sorted[count / 2]) / 2m;

        // Sample Standard Deviation
        decimal standardDeviation = 0m;
        if (count > 1)
        {
            var sumOfSquares = readings.Sum(x => (double)((x - mean) * (x - mean)));
            standardDeviation = (decimal)Math.Sqrt(sumOfSquares / (count - 1));
        }

        // Glucose Management Indicator (GMI) formula: 3.31 + (0.02392 * mean)
        var gmi = Math.Round(3.31m + (0.02392m * mean), 2);

        // Coefficient of Variation (CV) %
        var cvPercentage = mean > 0 ? Math.Round((standardDeviation / mean) * 100m, 2) : 0m;

        // Clinical Target Ranges: Low < 70, Target 70-180, High > 180
        var inRangeCount = readings.Count(x => x >= 70m && x <= 180m);
        var lowCount = readings.Count(x => x < 70m);
        var highCount = readings.Count(x => x > 180m);

        // Standard CGM reading interval is approximately 4 minutes
        var timeInRangeMinutes = inRangeCount * 4;
        var timeBelowRangeMinutes = lowCount * 4;
        var timeAboveRangeMinutes = highCount * 4;

        var inRangePercent = Math.Round(((decimal)inRangeCount / count) * 100m, 2);
        var lowPercent = Math.Round(((decimal)lowCount / count) * 100m, 2);
        var highPercent = Math.Round(((decimal)highCount / count) * 100m, 2);

        // Detect discrete excursion events (transitions into <70 or >180)
        int highEvents = 0;
        int lowEvents = 0;
        bool inHigh = false;
        bool inLow = false;

        foreach (var r in readings)
        {
            if (r > 180m)
            {
                if (!inHigh)
                {
                    highEvents++;
                    inHigh = true;
                }
            }
            else
            {
                inHigh = false;
            }

            if (r < 70m)
            {
                if (!inLow)
                {
                    lowEvents++;
                    inLow = true;
                }
            }
            else
            {
                inLow = false;
            }
        }

        return new DailyGlucoseSummaryEntity
        {
            UserId = userId,
            SummaryDate = date,
            ReadingCount = count,
            MeanGlucose = Math.Round(mean, 2),
            MedianGlucose = Math.Round(median, 2),
            StandardDeviation = Math.Round(standardDeviation, 2),
            Gmi = gmi,
            CvPercentage = cvPercentage,
            TimeInRangeMinutes = timeInRangeMinutes,
            TimeBelowRangeMinutes = timeBelowRangeMinutes,
            TimeAboveRangeMinutes = timeAboveRangeMinutes,
            TimeInRangePercentage = inRangePercent,
            TimeBelowRangePercentage = lowPercent,
            TimeAboveRangePercentage = highPercent,
            LowestGlucose = readings.Min(),
            HighestGlucose = readings.Max(),
            HighEventsCount = highEvents,
            LowEventsCount = lowEvents,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
