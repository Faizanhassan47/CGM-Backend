using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace CGM.Api.Models.Entities;
[Table("AlertQueue", Schema="dbo")]
public sealed class AlertQueueEntity
{
    [Key] public long Id { get; set; }
    public long AlertId { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    [MaxLength(500)] public string? ErrorMessage { get; set; }
    [ForeignKey(nameof(AlertId))] public AlertEntity Alert { get; set; } = null!;
}
