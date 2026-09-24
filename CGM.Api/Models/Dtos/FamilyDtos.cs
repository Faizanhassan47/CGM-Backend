using System.ComponentModel.DataAnnotations;

namespace CGM.Api.Models.Dtos;

public record UpdateCareCircleRoleRequest(string Role);

public record CreateFamilyRequest([Required, MaxLength(150)] string FamilyName);
public record JoinFamilyRequest([Required] string ReferralCode);
public record UpdateFamilyAlertPreferenceRequest(bool ReceiveAlerts);
public record UpdateFamilyThresholdsRequest(decimal LowGlucoseThreshold, decimal HighGlucoseThreshold);
public record FamilyMemberResponse(int UserId, string FullName, string Email, string Role, bool ReceiveAlerts, string Status, DateTime JoinedAt);
public record FamilyResponse(int FamilyId, string FamilyName, int OwnerUserId, int CurrentUserId, bool IsOwner, string MyReferralCode, decimal LowGlucoseThreshold, decimal HighGlucoseThreshold, IReadOnlyList<FamilyMemberResponse> Members);

public record SendNotificationRequest([Required] string Message, [Required] string Target);
public record WeeklyReportItemResponse(int UserId, string FullName, string AlertType, decimal? GlucoseValue, DateTime TriggeredAt);
