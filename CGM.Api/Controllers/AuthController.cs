using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using CGM.Api.Services;
using CGM.Api.Services.Email;
using Google.Apis.Auth;

namespace CGM.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CgmDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IReferralCodeService _referralCodes;
    private readonly IConfiguration _configuration;

    public AuthController(
        CgmDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailService emailService,
        IReferralCodeService referralCodes,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailService = emailService;
        _referralCodes = referralCodes;
        _configuration = configuration;
    }

    [HttpPost("social")]
    public async Task<ActionResult<AuthResponseDto>> SocialLogin([FromBody] SocialAuthRequestDto request)
    {
        if (!string.Equals(request.Provider, "Google", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new AuthResponseDto(false, "This social provider is not supported.", null, null, null, null));

        var clientId = _configuration["GoogleAuthentication:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(503, new AuthResponseDto(false, "Google authentication is not configured.", null, null, null, null));

        GoogleJsonWebSignature.Payload google;
        try
        {
            google = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (Exception)
        {
            return Unauthorized(new AuthResponseDto(false, "The Google sign-in token is invalid or expired.", null, null, null, null));
        }

        if (string.IsNullOrWhiteSpace(google.Subject) || string.IsNullOrWhiteSpace(google.Email) || google.EmailVerified != true)
            return Unauthorized(new AuthResponseDto(false, "Google did not provide a verified email address.", null, null, null, null));

        var email = google.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == google.Subject || u.Email == email);

        if (user is null)
        {
            user = new User
            {
                FullName = string.IsNullOrWhiteSpace(google.Name) ? email.Split('@')[0] : google.Name.Trim(),
                Email = email,
                AuthProvider = "Google",
                GoogleSubjectId = google.Subject,
                EmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                Profile = new PatientProfileEntity
                {
                    PreferredGlucoseUnit = "mg/dL",
                    Language = "English",
                    Theme = "System",
                    ProfileCompleted = false,
                    CreatedAt = DateTime.UtcNow
                }
            };
            user.ReferralCode = await _referralCodes.GenerateUniqueAsync(HttpContext.RequestAborted);
            _db.Users.Add(user);
        }
        else
        {
            if (!user.IsActive)
                return Unauthorized(new AuthResponseDto(false, "This account is inactive.", null, null, null, null));
            if (!string.IsNullOrWhiteSpace(user.GoogleSubjectId) && user.GoogleSubjectId != google.Subject)
                return Conflict(new AuthResponseDto(false, "This email is linked to another Google account.", null, null, null, null));

            user.GoogleSubjectId = google.Subject;
            user.EmailVerified = true;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId))
            user.LastLoginDeviceId = request.DeviceId;
        if (!string.IsNullOrWhiteSpace(request.DeviceInfo))
            user.LastLoginDeviceInfo = request.DeviceInfo;

        await _db.SaveChangesAsync();

        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, request.DeviceId);
        var (rawRefreshToken, _) = _tokenService.GenerateRefreshToken(user, request.DeviceId);

        var userDto = new UserDto(user.Id, user.FullName, user.Email, user.AuthProvider,
            user.EmailVerified, user.Profile?.ProfileCompleted ?? false,
            user.Profile?.PreferredGlucoseUnit ?? "mg/dL", user.ReferralCode);
        return Ok(new AuthResponseDto(true, "Signed in with Google.", accessToken, rawRefreshToken, expiresAt, userDto));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return BadRequest(new AuthResponseDto(false, "An account with this email address already exists.", null, null, null, null));
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLower(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            AuthProvider = "Email",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.ReferralCode = await _referralCodes.GenerateUniqueAsync(HttpContext.RequestAborted);

        var profile = new PatientProfileEntity
        {
            User = user,
            PhoneNumber = request.PhoneNumber,
            PreferredGlucoseUnit = request.PreferredGlucoseUnit,
            Language = "English",
            Theme = "System",
            ProfileCompleted = true,
            CreatedAt = DateTime.UtcNow
        };

        user.Profile = profile;

        _db.Users.Add(user);
        for (var attempt = 0; ; attempt++)
        {
            try { await _db.SaveChangesAsync(); break; }
            catch (DbUpdateException ex) when (attempt < 4 && ex.InnerException?.Message.Contains("UX_Users_ReferralCode", StringComparison.OrdinalIgnoreCase) == true)
            {
                user.ReferralCode = await _referralCodes.GenerateUniqueAsync(HttpContext.RequestAborted);
            }
        }

        // Send Welcome email in background without blocking
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName); } catch { }
        });

        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, _) = _tokenService.GenerateRefreshToken(user);

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            profile.ProfileCompleted,
            profile.PreferredGlucoseUnit,
            user.ReferralCode
        );

        var message = "Account created successfully.";
        return Ok(new AuthResponseDto(true, message, accessToken, rawRefreshToken, expiresAt, userDto));
    }



    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new AuthResponseDto(false, "Invalid email or password.", null, null, null, null));
        }

        if (!user.IsActive)
        {
            return Unauthorized(new AuthResponseDto(false, "This account is inactive.", null, null, null, null));
        }

        user.LastLoginAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
            user.LastLoginDeviceId = request.DeviceId;
        if (!string.IsNullOrWhiteSpace(request.DeviceInfo))
            user.LastLoginDeviceInfo = request.DeviceInfo;

        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, request.DeviceId);
        var (rawRefreshToken, _) = _tokenService.GenerateRefreshToken(user, request.DeviceId);
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            user.Profile?.ProfileCompleted ?? false,
            user.Profile?.PreferredGlucoseUnit ?? "mg/dL",
            user.ReferralCode
        );

        return Ok(new AuthResponseDto(true, "Login successful.", accessToken, rawRefreshToken, expiresAt, userDto));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null)
            return Ok(new ResetPasswordResponseDto(true, "If an account exists, password reset instructions have been sent."));

        // Invalidate any existing unused reset tokens for this email
        var existingTokens = await _db.PasswordResetTokens
            .Where(t => t.Email.ToLower() == email && !t.IsUsed)
            .ToListAsync();

        foreach (var t in existingTokens)
        {
            t.IsUsed = true;
        }

        // Generate 6-digit OTP code and secure token
        var otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(15); // Strict 15-minute expiration

        var resetEntity = new PasswordResetTokenEntity
        {
            UserId = user.Id,
            Email = email,
            Token = _tokenService.HashToken(token),
            OtpCode = _tokenService.HashToken(otpCode),
            ExpiresAt = expiresAt,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.PasswordResetTokens.Add(resetEntity);
        await _db.SaveChangesAsync();

        // Send Email via SMTP in background so mobile app responds immediately (<50ms)
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, token, otpCode, expiresAt);
            }
            catch (Exception)
            {
                // Suppress exception; logged inside EmailService
            }
        });

        return Ok(new ResetPasswordResponseDto(true,
            "If an account exists, password reset instructions have been sent."));
    }

    [HttpPost("verify-reset-code")]
    public async Task<ActionResult<ResetPasswordResponseDto>> VerifyResetCode([FromBody] VerifyResetCodeRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var code = request.Code.Trim();
        var codeHash = _tokenService.HashToken(code);

        var tokenEntity = await _db.PasswordResetTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == email && (t.OtpCode == codeHash || t.OtpCode == code) && !t.IsUsed);

        if (tokenEntity == null)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "Invalid reset code. Please check your email and try again."));
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "This reset code has expired (15-minute limit). Please request a new code."));
        }

        return Ok(new ResetPasswordResponseDto(true, "Reset code verified successfully."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var tokenOrCode = request.TokenOrCode.Trim();
        var tokenHash = _tokenService.HashToken(tokenOrCode);

        var tokenEntity = await _db.PasswordResetTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == email && (t.Token == tokenHash || t.OtpCode == tokenHash || t.OtpCode == tokenOrCode || t.Token == tokenOrCode) && !t.IsUsed);

        if (tokenEntity == null)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "Invalid password reset session. Please request a new reset code."));
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "This password reset session has expired (15-minute limit). Please request a new reset code."));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            return NotFound(new ResetPasswordResponseDto(false, "User account could not be found."));
        }

        // Update password hash
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        user.TokenVersion++;

        // Invalidate token
        tokenEntity.IsUsed = true;
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ResetPasswordResponseDto(true, "Your password has been successfully updated. You can now sign in with your new password."));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var principal = _tokenService.ValidateRefreshToken(request.RefreshToken);
        var userIdClaim = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        var versionClaim = principal?.FindFirstValue("token_version");
        if (!int.TryParse(userIdClaim, out var userId) || !int.TryParse(versionClaim, out var tokenVersion))
        {
            return Unauthorized(new AuthResponseDto(false, "Invalid or expired refresh token.", null, null, null, null));
        }

        var user = await _db.Users.Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || user.TokenVersion != tokenVersion)
            return Unauthorized(new AuthResponseDto(false, "Invalid or expired refresh token.", null, null, null, null));

        var deviceId = principal?.FindFirstValue("device_id");
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, deviceId);

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            user.Profile?.ProfileCompleted ?? false,
            user.Profile?.PreferredGlucoseUnit ?? "mg/dL",
            user.ReferralCode
        );

        // Return the same refresh token so the session has a fixed seven-day lifetime.
        return Ok(new AuthResponseDto(true, "Token refreshed.", accessToken, request.RefreshToken, expiresAt, userDto));
    }

    [HttpPost("logout")]
    public IActionResult Logout([FromBody] RefreshTokenRequestDto request)
    {
        return Ok(new { message = "Logged out. The client must delete its locally stored tokens." });
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();
        await _db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TokenVersion, x => x.TokenVersion + 1), cancellationToken);
        return Ok(new { message = "All sessions have been revoked." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "Password changes are unavailable for this account."));

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "Current password is incorrect."));

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "New password must be different from the current password."));

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        user.TokenVersion++;

        await _db.SaveChangesAsync();
        return Ok(new ResetPasswordResponseDto(true, "Password changed successfully. Please sign in again."));
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return NotFound();

        // Delete explicitly in dependency order. Some deployed databases use
        // restrictive foreign keys even where the EF model specifies cascade.
        var email = user.Email;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Delete join/dependent rows first. Production databases can have
            // restrictive FKs even when the current EF model specifies cascade.
            var ownedAlertIds = _db.Alerts.Where(x => x.UserId == userId).Select(x => x.Id);
            await _db.AlertRecipients
                .Where(x => x.UserId == userId || ownedAlertIds.Contains(x.AlertId))
                .ExecuteDeleteAsync(cancellationToken);
            await _db.AlertDeliveryHistory.Where(x => ownedAlertIds.Contains(x.AlertId)).ExecuteDeleteAsync(cancellationToken);
            await _db.AlertQueue.Where(x => ownedAlertIds.Contains(x.AlertId)).ExecuteDeleteAsync(cancellationToken);

            var ownedFamilyIds = _db.Families.Where(x => x.OwnerUserId == userId).Select(x => x.Id);
            await _db.FamilyMembers
                .Where(x => x.UserId == userId || ownedFamilyIds.Contains(x.FamilyId))
                .ExecuteDeleteAsync(cancellationToken);
            await _db.Families
                .Where(x => x.OwnerUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _db.Alerts.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.GlucoseMeasurements.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.Sensors.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.CgmDevices.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.PatientProfiles.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.PasswordResetTokens
                .Where(x => x.UserId == userId || x.Email == email)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.AlertRules.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.NotificationEndpoints.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.DailyGlucoseSummaries.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            await _db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
        return NoContent();
    }
}
