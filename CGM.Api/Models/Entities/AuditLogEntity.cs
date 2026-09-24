using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("AuditLogs", Schema = "dbo")]
public sealed class AuditLogEntity
{
    [Key] public long Id { get; set; }
    public int? UserId { get; set; }
    [MaxLength(50)] public string Action { get; set; } = string.Empty;
    [MaxLength(100)] public string Entity { get; set; } = string.Empty;
    [MaxLength(100)] public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    [MaxLength(100)] public string? IpAddress { get; set; }
    [MaxLength(100)] public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
