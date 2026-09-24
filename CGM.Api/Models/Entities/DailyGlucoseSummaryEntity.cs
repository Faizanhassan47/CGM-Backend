using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("DailyGlucoseSummaries", Schema = "reporting")]
public sealed class DailyGlucoseSummaryEntity
{
    [Key]
    public long Id { get; set; }

    public int UserId { get; set; }

    public DateOnly SummaryDate { get; set; }

    public int ReadingCount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MeanGlucose { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MedianGlucose { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal StandardDeviation { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Gmi { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal CvPercentage { get; set; }

    public int TimeInRangeMinutes { get; set; }

    public int TimeBelowRangeMinutes { get; set; }

    public int TimeAboveRangeMinutes { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TimeInRangePercentage { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TimeBelowRangePercentage { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TimeAboveRangePercentage { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal LowestGlucose { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal HighestGlucose { get; set; }

    public int HighEventsCount { get; set; }

    public int LowEventsCount { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
