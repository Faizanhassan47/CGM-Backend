using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("CGMDevices", Schema = "dbo")]
public class CgmDeviceEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [MaxLength(150)]
    public string? DeviceName { get; set; }

    [MaxLength(50)]
    public string? DeviceType { get; set; } // Disposable / Reusable

    [MaxLength(50)]
    public string? DeviceModel { get; set; } // G-AA / G-PA0 / G-PA1

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(150)]
    public string? BleDeviceName { get; set; }

    [MaxLength(50)]
    public string? FirmwareVersion { get; set; }

    public int? BatteryVoltageMv { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? BatteryPercentage { get; set; }

    [Required]
    [MaxLength(30)]
    public string ConnectionStatus { get; set; } = "Disconnected";

    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastCommunicationAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    public virtual ICollection<SensorEntity> Sensors { get; set; } = new List<SensorEntity>();
    public virtual ICollection<GlucoseMeasurementEntity> Measurements { get; set; } = new List<GlucoseMeasurementEntity>();
}
