using System;
using System.Collections.Generic;

namespace CGM.Api.Models.Dtos;

public class DetailedReportDto
{
    public int UserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalReadings { get; set; }
    
    // Core Glycemic Metrics
    public string AvgGlucose { get; set; } = "-";
    public string TimeInRange { get; set; } = "-";
    public string TimeAboveRange { get; set; } = "-";
    public string TimeBelowRange { get; set; } = "-";
    public string TimeVeryHigh { get; set; } = "-";
    public string TimeVeryLow { get; set; } = "-";
    public string HighestGlucose { get; set; } = "-";
    public string LowestGlucose { get; set; } = "-";
    public string EstimatedA1c { get; set; } = "-";
    public string GlucoseVariability { get; set; } = "-";
    public string StandardDeviation { get; set; } = "-";

    // Numerical percentages for charts / progress bars
    public double TirPercentage { get; set; }
    public double TarPercentage { get; set; }
    public double TbrPercentage { get; set; }
    public double VeryHighPercentage { get; set; }
    public double VeryLowPercentage { get; set; }

    // Daily breakdown for table
    public List<DailyReportBreakdownDto> DailySummaries { get; set; } = new();

    // Recent readings for detailed log
    public List<ReportReadingItemDto> RecentReadings { get; set; } = new();
}

public class DailyReportBreakdownDto
{
    public string DateFormatted { get; set; } = string.Empty;
    public int ReadingsCount { get; set; }
    public string AvgGlucose { get; set; } = "-";
    public string MinGlucose { get; set; } = "-";
    public string MaxGlucose { get; set; } = "-";
    public string TimeInRange { get; set; } = "-";
    public string Status { get; set; } = "Normal";
}

public class ReportReadingItemDto
{
    public DateTime Time { get; set; }
    public string TimeFormatted { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Status { get; set; } = "Normal";
}
