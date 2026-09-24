using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace CGM.Api.Models.Entities;
[Table("AlertDeliveryHistory", Schema="dbo")]
public sealed class AlertDeliveryHistoryEntity
{
    [Key] public long Id { get; set; }
    public long AlertId { get; set; }
    [MaxLength(30)] public string Channel { get; set; } = string.Empty;
    [MaxLength(255)] public string? Destination { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    [MaxLength(500)] public string? FailureReason { get; set; }
}
