using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace CGM.Api.Models.Entities;
[Table("AlertRules", Schema="dbo")]
public sealed class AlertRuleEntity
{
    [Key] public long Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(50)] public string AlertType { get; set; } = string.Empty;
    [Column(TypeName="decimal(10,2)")] public decimal? MinimumValue { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal? MaximumValue { get; set; }
    public int DurationMinutes { get; set; } = 15;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
