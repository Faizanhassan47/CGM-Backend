using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("GlucoseMeasurements", Schema = "dbo")]
public class GlucoseMeasurementEntity
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public int SensorId { get; set; }

    public int? DeviceId { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? GlucoseValue { get; set; }

    [Required]
    public DateTime MeasurementTime { get; set; }

    public int? BatteryVoltageMv { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? DeviceTemperatureC { get; set; }

    [Column(TypeName = "decimal(12,4)")]
    public decimal? WE1CurrentNa { get; set; }

    public bool IsSynced { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(SensorId))]
    public virtual SensorEntity Sensor { get; set; } = null!;

    [ForeignKey(nameof(DeviceId))]
    public virtual CgmDeviceEntity? Device { get; set; }
}
