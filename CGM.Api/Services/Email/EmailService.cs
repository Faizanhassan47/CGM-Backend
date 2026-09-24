using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CGM.Api.Services.Email;

public interface IEmailService
{
    Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName);

    Task<bool> SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, string otpCode, DateTime expiresAt);
    Task<bool> SendGlucoseAlertEmailAsync(string toEmail, string recipientName, string patientName, decimal glucoseValue, string unit, decimal threshold, bool isLow);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string? SmtpHost => Environment.GetEnvironmentVariable("SMTP_HOST") ?? _configuration["Smtp:Host"];
    private int SmtpPort => int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? _configuration["Smtp:Port"], out var port) ? port : 465;
    private bool SmtpSsl => bool.TryParse(Environment.GetEnvironmentVariable("SMTP_SSL") ?? _configuration["Smtp:Ssl"], out var ssl) ? ssl : true;
    private string? SmtpUser => Environment.GetEnvironmentVariable("SMTP_USER") ?? _configuration["Smtp:User"];
    private string? SmtpPass => Environment.GetEnvironmentVariable("SMTP_PASS") ?? _configuration["Smtp:Pass"];
    private string FromEmail => Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? _configuration["Smtp:FromEmail"] ?? "no-reply@glucotrack.local";
    private string FromName => Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? _configuration["Smtp:FromName"] ?? "GlucoTrack CGM Platform";
    private string ResetUrlBase => Environment.GetEnvironmentVariable("APP_RESET_URL") ?? "https://cgm.gms-world.co/reset-password";

    public async Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName)
    {
        var subject = "Welcome to GlucoTrack CGM — Your Account is Ready";
        var bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #F8FAF9; margin: 0; padding: 20px; }}
        .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.05); border: 1px solid #E6F0EB; }}
        .header {{ background-color: #01B4F1; padding: 32px 24px; text-align: center; }}
        .header h1 {{ color: #ffffff; margin: 0; font-size: 24px; font-weight: 700; }}
        .header p {{ color: #E6F4EA; margin: 6px 0 0 0; font-size: 14px; }}
        .content {{ padding: 32px 28px; color: #0F172A; line-height: 1.6; }}
        .greeting {{ font-size: 18px; font-weight: 600; margin-bottom: 12px; }}
        .card {{ background: #F0FDFA; border: 1px solid #CCFBF1; border-radius: 12px; padding: 18px; margin: 20px 0; }}
        .btn {{ display: inline-block; background-color: #01B4F1; color: #ffffff !important; text-decoration: none; padding: 14px 28px; border-radius: 10px; font-weight: bold; font-size: 14px; text-align: center; margin-top: 10px; }}
        .footer {{ background: #F8FAF9; padding: 20px; text-align: center; font-size: 12px; color: #64748B; border-top: 1px solid #E6F0EB; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>GlucoTrack CGM</h1>
            <p>Certified Continuous Glucose Monitoring System</p>
        </div>
        <div class='content'>
            <div class='greeting'>Hello, {fullName}! 👋</div>
            <p>Welcome to GlucoTrack. Your account has been successfully created and linked to the Continuous Glucose Monitoring platform.</p>
            
            <div class='card'>
                <h3 style='margin:0 0 8px 0; color:#01B4F1; font-size:15px;'>📱 What's Next?</h3>
                <ul style='margin:0; padding-left:20px; font-size:13px; color:#334155;'>
                    <li>Open the mobile app and complete your patient profile</li>
                    <li>Pair your disposable CGM sensor via Bluetooth Low Energy</li>
                    <li>Monitor real-time glucose trends and receive instant alerts</li>
                </ul>
            </div>

            <p style='font-size:13px; color:#64748B;'>If you have any questions or require clinical assistance, our support team is available 24/7.</p>
        </div>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} GlucoTrack Medical Systems. Confidential &amp; Secure.
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(toEmail, fullName, subject, bodyHtml);
    }



    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, string otpCode, DateTime expiresAt)
    {
        _logger.LogInformation("🔑 [PASSWORD RESET OTP] Target: {Email} | OTP Code: {OtpCode} | Expires: {ExpiresAt:HH:mm:ss} UTC", toEmail, otpCode, expiresAt);
        var subject = "PASSWORD RESET - CGM Patient App";

        var textBody = $@"PASSWORD RESET

We received a request to reset the password for your CGM Patient App account.

Your password reset OTP code is:

{otpCode}

This OTP code is valid for 15 minutes.

Please do not share this code with anyone.

If you did not request a password reset, you can safely ignore this email. Your password will remain unchanged.";

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
</head>
<body style=""font-family: Arial, Helvetica, sans-serif; background-color: #F9FAFB; padding: 20px; margin: 0;"">
    <div style=""max-width: 560px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; border: 1px solid #E5E7EB; color: #111827; line-height: 1.6;"">
        <h2 style=""margin: 0 0 18px 0; font-size: 18px; font-weight: bold; color: #111827;"">PASSWORD RESET</h2>
        
        <p style=""margin: 0 0 16px 0; font-size: 14px; color: #374151;"">
            We received a request to reset the password for your CGM Patient App account.
        </p>
        
        <p style=""margin: 0 0 8px 0; font-size: 14px; color: #374151;"">
            Your password reset OTP code is:
        </p>
        
        <div style=""font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #111827; margin: 14px 0 18px 0;"">
            {otpCode}
        </div>
        
        <p style=""margin: 0 0 8px 0; font-size: 14px; color: #374151;"">
            This OTP code is valid for 15 minutes.
        </p>
        
        <p style=""margin: 0 0 18px 0; font-size: 14px; color: #374151;"">
            Please do not share this code with anyone.
        </p>
        
        <p style=""margin: 0; font-size: 13px; color: #6B7280;"">
            If you did not request a password reset, you can safely ignore this email. Your password will remain unchanged.
        </p>
    </div>
</body>
</html>";

        return await SendEmailAsync(toEmail, fullName, subject, htmlBody, textBody);
    }

    public Task<bool> SendGlucoseAlertEmailAsync(string toEmail, string recipientName, string patientName, decimal glucoseValue, string unit, decimal threshold, bool isLow)
    {
        var kind = isLow ? "Low" : "High";
        var direction = isLow ? "below" : "above";
        var subject = $"{kind} Glucose Alert - {patientName}";
        var text = $"{kind} Glucose Alert\n\n{patientName}'s glucose is {glucoseValue:0.##} {unit}, {direction} the family alert threshold of {threshold:0.##} {unit}.\n\nOpen GlucoTrack for details.";
        var html = $"<div style='font-family:Arial;max-width:560px;padding:24px'><h2>{kind} Glucose Alert</h2><p>Hello {System.Net.WebUtility.HtmlEncode(recipientName)},</p><p><strong>{System.Net.WebUtility.HtmlEncode(patientName)}</strong>'s glucose is <strong>{glucoseValue:0.##} {unit}</strong>, {direction} the family alert threshold of {threshold:0.##} {unit}.</p><p>Open GlucoTrack for details.</p></div>";
        return SendEmailAsync(toEmail, recipientName, subject, html, text);
    }

    private async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string bodyHtml, string? textBody = null)
    {
        var host = SmtpHost;
        var user = SmtpUser;
        var pass = SmtpPass;
        var port = SmtpPort;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            _logger.LogWarning("[DEV/MOCK EMAIL] SMTP credentials not configured. Outgoing email to {ToEmail} ({Subject}):\n{Content}",
                toEmail, subject, textBody ?? bodyHtml);
            return true;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FromName, FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = bodyHtml,
                TextBody = textBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Accept certificate if needed or use SSL
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var secureSocket = port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(host, port, secureSocket);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Successfully sent email '{Subject}' to {ToEmail}", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email '{Subject}' to {ToEmail} via {Host}:{Port}", subject, toEmail, host ?? "unconfigured", port);
            return false;
        }
    }
}
