using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("Alerts", Schema = "dbo")]
public class AlertEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? SensorId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AlertType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? GlucoseValue { get; set; }

    [MaxLength(10)]
    public string? GlucoseUnit { get; set; }

    [MaxLength(20)]
    public string? Severity { get; set; } // Info / Warning / Critical

    public bool IsRead { get; set; } = false;

    public DateTime AlertTime { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(SensorId))]
    public virtual SensorEntity? Sensor { get; set; }

    public virtual ICollection<AlertRecipientEntity> Recipients { get; set; } = new List<AlertRecipientEntity>();
}
