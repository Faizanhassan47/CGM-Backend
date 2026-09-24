using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("AlertHistory", Schema = "dbo")]
public sealed class AlertHistoryEntity
{
    [Key] public long Id { get; set; }
    public long AlertId { get; set; }
    public int UserId { get; set; }
    [MaxLength(50)] public string AlertType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,2)")] public decimal? Threshold { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    [ForeignKey(nameof(AlertId))] public AlertEntity Alert { get; set; } = null!;
}
