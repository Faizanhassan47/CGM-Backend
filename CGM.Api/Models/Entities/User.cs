using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("Users", Schema = "dbo")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    [Required]
    [MaxLength(30)]
    public string AuthProvider { get; set; } = "Email";

    [MaxLength(255)]
    public string? GoogleSubjectId { get; set; }

    [MaxLength(255)]
    public string? AppleSubjectId { get; set; }

    [MaxLength(255)]
    public string? LastLoginDeviceId { get; set; }

    [MaxLength(255)]
    public string? LastLoginDeviceInfo { get; set; }

    public bool EmailVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [MaxLength(12)]
    public string? ReferralCode { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int TokenVersion { get; set; }

    // Navigation Properties
    public virtual PatientProfileEntity? Profile { get; set; }
    public virtual ICollection<CgmDeviceEntity> Devices { get; set; } = new List<CgmDeviceEntity>();
    public virtual ICollection<SensorEntity> Sensors { get; set; } = new List<SensorEntity>();
    public virtual ICollection<GlucoseMeasurementEntity> Measurements { get; set; } = new List<GlucoseMeasurementEntity>();
    public virtual ICollection<AlertEntity> Alerts { get; set; } = new List<AlertEntity>();
    public virtual ICollection<FamilyMemberEntity> FamilyMemberships { get; set; } = new List<FamilyMemberEntity>();
    public virtual ICollection<AlertRecipientEntity> AlertRecipients { get; set; } = new List<AlertRecipientEntity>();
}
