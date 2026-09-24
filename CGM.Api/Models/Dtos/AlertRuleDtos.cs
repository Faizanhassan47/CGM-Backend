using System.ComponentModel.DataAnnotations;
namespace CGM.Api.Models.Dtos;
public record SaveAlertRuleRequest(
    [Required, MaxLength(50)] string AlertType,
    decimal? MinimumValue,
    decimal? MaximumValue,
    [Range(1, 1440)] int DurationMinutes,
    bool Enabled);
