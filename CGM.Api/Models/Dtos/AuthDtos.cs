using System.ComponentModel.DataAnnotations;

namespace CGM.Api.Models.Dtos;

public record RegisterRequestDto(
    [Required] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    string? PhoneNumber,
    string PreferredGlucoseUnit = "mg/dL"
);

public record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false,
    string? DeviceInfo = null,
    string? DeviceId = null,
    string? DeviceName = null,
    string? Platform = null
);

public record SocialAuthRequestDto(
    [Required] string IdToken,
    [Required] string Provider, // Google / Apple
    string? FullName,
    string? DeviceInfo,
    string? DeviceId = null,
    string? DeviceName = null,
    string? Platform = null
);

public record RefreshTokenRequestDto(
    [Required] string RefreshToken
);

public record AuthResponseDto(
    bool Success,
    string Message,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserDto? User
);

public record UserDto(
    int Id,
    string FullName,
    string Email,
    string AuthProvider,
    bool EmailVerified,
    bool ProfileCompleted,
    string PreferredGlucoseUnit,
    string? ReferralCode
);

public record ForgotPasswordRequestDto(
    [Required, EmailAddress] string Email
);

public record VerifyResetCodeRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Code
);

public record ResetPasswordRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string TokenOrCode,
    [Required, MinLength(6)] string NewPassword
);

public record ChangePasswordRequestDto(
    [Required] string CurrentPassword,
    [Required, MinLength(6)] string NewPassword
);

public record ResetPasswordResponseDto(
    bool Success,
    string Message
);

public record VerifyEmailRequestDto(
    [Required, EmailAddress] string Email,
    [Required, StringLength(6, MinimumLength = 6)] string Code
);

public record ResendVerificationRequestDto(
    [Required, EmailAddress] string Email
);

public record PatientProfileDto(
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string PreferredGlucoseUnit,
    bool ProfileCompleted,
    string Theme,
    string Language
);

public record UpdatePatientProfileDto(
    [Required] string FullName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    [Required] string PreferredGlucoseUnit,
    string Theme = "System",
    string Language = "English"
);
