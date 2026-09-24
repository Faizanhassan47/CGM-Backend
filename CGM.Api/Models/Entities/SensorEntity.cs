using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("Sensors", Schema = "dbo")]
public class SensorEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int DeviceId { get; set; }

    [MaxLength(150)]
    public string? SensorIdentifier { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Inactive"; // Active / Warmup / EndingSoon / Expired / Inactive

    public DateTime? StartedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? LastReadingAt { get; set; }


    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(DeviceId))]
    public virtual CgmDeviceEntity Device { get; set; } = null!;

    public virtual ICollection<GlucoseMeasurementEntity> Measurements { get; set; } = new List<GlucoseMeasurementEntity>();
    public virtual ICollection<AlertEntity> Alerts { get; set; } = new List<AlertEntity>();
}
