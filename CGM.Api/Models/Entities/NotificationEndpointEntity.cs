using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace CGM.Api.Models.Entities;
[Table("NotificationEndpoints",Schema="dbo")]
public sealed class NotificationEndpointEntity
{
    [Key] public long Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(30)] public string Channel { get; set; } = string.Empty;
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
