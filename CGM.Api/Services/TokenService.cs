using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CGM.Api.Models.Entities;

namespace CGM.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, string? deviceId = null);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken(User user, string? deviceId = null);
    ClaimsPrincipal? ValidateRefreshToken(string token);
    string HashToken(string token);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, string? deviceId = null)
    {
        var secret = _configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET is required.");
        var issuer = _configuration["JWT_ISSUER"] ?? "CGM.Api";
        var audience = _configuration["JWT_AUDIENCE"] ?? "CGM.PatientApp";
        var expiryMinutes = int.TryParse(_configuration["JWT_EXPIRY_MINUTES"], out var mins) ? mins : 15;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("auth_provider", user.AuthProvider),
            new(ClaimTypes.Role, "Patient"),
            new("device_id", deviceId ?? "unknown")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public (string Token, DateTime ExpiresAt) GenerateRefreshToken(User user, string? deviceId = null)
    {
        var secret = _configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET is required.");
        var issuer = _configuration["JWT_ISSUER"] ?? "CGM.Api";
        var audience = _configuration["JWT_AUDIENCE"] ?? "CGM.PatientApp";
        var expiryDays = int.TryParse(_configuration["REFRESH_TOKEN_EXPIRY_DAYS"], out var days) ? days : 7;
        var expiresAt = DateTime.UtcNow.AddDays(expiryDays);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("token_type", "refresh"),
                new Claim("token_version", user.TokenVersion.ToString()),
                new Claim("device_id", deviceId ?? "unknown")
            ],
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        try
        {
            var secret = _configuration["JWT_SECRET"]
                ?? throw new InvalidOperationException("JWT_SECRET is required.");
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["JWT_ISSUER"] ?? "CGM.Api",
                ValidAudience = _configuration["JWT_AUDIENCE"] ?? "CGM.PatientApp",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            return validatedToken is JwtSecurityToken jwt
                && jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.Ordinal)
                && principal.FindFirstValue("token_type") == "refresh" ? principal : null;
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
    }

    public string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
