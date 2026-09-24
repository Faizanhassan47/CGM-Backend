using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("PasswordResetRequests", Schema = "dbo")]
public class PasswordResetTokenEntity
{
    [Key]
    public int Id { get; set; }

    public int? UserId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string OtpCode { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
