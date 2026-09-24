using CGM.Api.Services;
using Xunit;

namespace CGM.Api.Tests;

public class DailySummaryWorkerTests
{
    [Fact]
    public void CalculateSummary_ValidReadings_CalculatesAccurateClinicalMetrics()
    {
        // 10 readings:
        // 60 (low), 80 (normal), 100 (normal), 110 (normal), 120 (normal),
        // 130 (normal), 140 (normal), 150 (normal), 190 (high), 210 (high)
        var readings = new List<decimal>
        {
            60m, 80m, 100m, 110m, 120m, 130m, 140m, 150m, 190m, 210m
        };

        var date = new DateOnly(2026, 9, 10);
        var summary = DailySummaryWorker.CalculateSummary(userId: 42, date, readings);

        Assert.Equal(42, summary.UserId);
        Assert.Equal(date, summary.SummaryDate);
        Assert.Equal(10, summary.ReadingCount);

        // Mean: (60 + 80 + 100 + 110 + 120 + 130 + 140 + 150 + 190 + 210) / 10 = 129.00
        Assert.Equal(129.00m, summary.MeanGlucose);

        // Median: (120 + 130) / 2 = 125.00
        Assert.Equal(125.00m, summary.MedianGlucose);

        // Min & Max
        Assert.Equal(60m, summary.LowestGlucose);
        Assert.Equal(210m, summary.HighestGlucose);

        // Standard Deviation > 0
        Assert.True(summary.StandardDeviation > 40m);

        // GMI formula: 3.31 + (0.02392 * 129) = 3.31 + 3.08568 = 6.40
        Assert.Equal(6.40m, summary.Gmi);

        // Time In Range (70 to 180): 7 items (80, 100, 110, 120, 130, 140, 150) = 70%
        Assert.Equal(70.00m, summary.TimeInRangePercentage);
        Assert.Equal(28, summary.TimeInRangeMinutes); // 7 * 4 mins = 28 mins

        // Time Below Range (< 70): 1 item (60) = 10%
        Assert.Equal(10.00m, summary.TimeBelowRangePercentage);
        Assert.Equal(4, summary.TimeBelowRangeMinutes);

        // Time Above Range (> 180): 2 items (190, 210) = 20%
        Assert.Equal(20.00m, summary.TimeAboveRangePercentage);
        Assert.Equal(8, summary.TimeAboveRangeMinutes);

        // High events = 1, Low events = 1
        Assert.Equal(1, summary.HighEventsCount);
        Assert.Equal(1, summary.LowEventsCount);
    }

    [Fact]
    public void CalculateSummary_EmptyReadings_ReturnsZeroValues()
    {
        var date = new DateOnly(2026, 9, 10);
        var summary = DailySummaryWorker.CalculateSummary(userId: 1, date, new List<decimal>());

        Assert.Equal(0, summary.ReadingCount);
        Assert.Equal(0m, summary.MeanGlucose);
    }
}
