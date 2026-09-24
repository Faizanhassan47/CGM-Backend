using CGM.Api.Models.Entities;
using CGM.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CGM.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(CgmDbContext context, IPasswordHasher passwordHasher)
    {
        try
        {
            if (context.Database.IsSqlServer())
            {
                await EnsureTablesExistAsync(context);
            }

            var testEmail = "faizanhassan47@gmail.com";
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == testEmail);

            if (existingUser == null)
            {
                var user = new User
                {
                    FullName = "Faizan Hassan",
                    Email = testEmail,
                    PasswordHash = passwordHasher.HashPassword("Test1234"),
                    AuthProvider = "Email",
                    EmailVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                var profile = new PatientProfileEntity
                {
                    UserId = user.Id,
                    PreferredGlucoseUnit = "mg/dL",
                    Language = "English",
                    Theme = "System",
                    ProfileCompleted = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.PatientProfiles.Add(profile);
                await context.SaveChangesAsync();

                Console.WriteLine($"[DbInitializer] Successfully seeded user: {testEmail}");
            }
            else
            {
                existingUser.PasswordHash = passwordHasher.HashPassword("Test1234");
                existingUser.EmailVerified = true;
                existingUser.IsActive = true;
                existingUser.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                Console.WriteLine($"[DbInitializer] Successfully updated user password for: {testEmail}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbInitializer] Error seeding database: {ex.Message}");
        }
    }

    private static async Task EnsureTablesExistAsync(CgmDbContext context)
    {
        const string sql = """
            IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AuditLogs (
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NULL,
                    Action NVARCHAR(50) NOT NULL,
                    Entity NVARCHAR(100) NOT NULL,
                    EntityId NVARCHAR(100) NULL,
                    OldValue NVARCHAR(MAX) NULL,
                    NewValue NVARCHAR(MAX) NULL,
                    IpAddress NVARCHAR(100) NULL,
                    CorrelationId NVARCHAR(100) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
                CREATE INDEX IX_AuditLogs_User_CreatedAt ON dbo.AuditLogs(UserId, CreatedAt DESC);
                CREATE INDEX IX_AuditLogs_Entity_EntityId ON dbo.AuditLogs(Entity, EntityId);
            END;

            IF OBJECT_ID('dbo.LoginHistory','U') IS NOT NULL DROP TABLE dbo.LoginHistory;

            IF OBJECT_ID('dbo.AlertHistory', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AlertHistory (
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    AlertId BIGINT NOT NULL,
                    UserId INT NOT NULL,
                    AlertType NVARCHAR(50) NOT NULL,
                    Threshold DECIMAL(10,2) NULL,
                    TriggeredAt DATETIME2 NOT NULL,
                    DeliveredAt DATETIME2 NULL,
                    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                    CONSTRAINT FK_AlertHistory_Alert FOREIGN KEY (AlertId) REFERENCES dbo.Alerts(Id) ON DELETE CASCADE);
                CREATE INDEX IX_AlertHistory_User_TriggeredAt ON dbo.AlertHistory(UserId, TriggeredAt DESC);
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'reporting') EXEC('CREATE SCHEMA reporting');
            IF OBJECT_ID('reporting.DailyGlucoseSummaries', 'U') IS NULL
            BEGIN
                CREATE TABLE reporting.DailyGlucoseSummaries (
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    SummaryDate DATE NOT NULL,
                    ReadingCount INT NOT NULL,
                    MeanGlucose DECIMAL(10,2) NOT NULL,
                    MedianGlucose DECIMAL(10,2) NOT NULL,
                    StandardDeviation DECIMAL(10,2) NOT NULL,
                    Gmi DECIMAL(10,2) NOT NULL,
                    CvPercentage DECIMAL(10,2) NOT NULL,
                    TimeInRangeMinutes INT NOT NULL,
                    TimeBelowRangeMinutes INT NOT NULL,
                    TimeAboveRangeMinutes INT NOT NULL,
                    TimeInRangePercentage DECIMAL(5,2) NOT NULL,
                    TimeBelowRangePercentage DECIMAL(5,2) NOT NULL,
                    TimeAboveRangePercentage DECIMAL(5,2) NOT NULL,
                    LowestGlucose DECIMAL(10,2) NOT NULL,
                    HighestGlucose DECIMAL(10,2) NOT NULL,
                    GeneratedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UX_DailyGlucoseSummaries_User_Date UNIQUE (UserId, SummaryDate));
                CREATE INDEX IX_DailyGlucoseSummaries_User_Date ON reporting.DailyGlucoseSummaries(UserId, SummaryDate DESC);
            END;

            IF COL_LENGTH('reporting.DailyGlucoseSummaries', 'HighEventsCount') IS NULL
                ALTER TABLE reporting.DailyGlucoseSummaries ADD HighEventsCount INT NOT NULL CONSTRAINT DF_DailyGlucoseSummaries_HighEvents DEFAULT 0;
            IF COL_LENGTH('reporting.DailyGlucoseSummaries', 'LowEventsCount') IS NULL
                ALTER TABLE reporting.DailyGlucoseSummaries ADD LowEventsCount INT NOT NULL CONSTRAINT DF_DailyGlucoseSummaries_LowEvents DEFAULT 0;

            IF OBJECT_ID('dbo.AlertRules','U') IS NULL
            BEGIN
                CREATE TABLE dbo.AlertRules(
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    AlertType NVARCHAR(50) NOT NULL,
                    MinimumValue DECIMAL(10,2) NULL,
                    MaximumValue DECIMAL(10,2) NULL,
                    DurationMinutes INT NOT NULL DEFAULT 15,
                    Enabled BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
                CREATE INDEX IX_AlertRules_User_Type_Enabled ON dbo.AlertRules(UserId,AlertType,Enabled);
            END;

            IF OBJECT_ID('dbo.AlertQueue','U') IS NULL
            BEGIN
                CREATE TABLE dbo.AlertQueue(
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    AlertId BIGINT NOT NULL,
                    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                    RetryCount INT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    NextAttemptAt DATETIME2 NULL,
                    ProcessedAt DATETIME2 NULL,
                    ErrorMessage NVARCHAR(500) NULL,
                    CONSTRAINT FK_AlertQueue_Alert FOREIGN KEY(AlertId) REFERENCES dbo.Alerts(Id) ON DELETE CASCADE);
                CREATE INDEX IX_AlertQueue_Status_NextAttempt ON dbo.AlertQueue(Status,NextAttemptAt);
            END;

            IF OBJECT_ID('dbo.AlertDeliveryHistory','U') IS NULL
            BEGIN
                CREATE TABLE dbo.AlertDeliveryHistory(
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    AlertId BIGINT NOT NULL,
                    Channel NVARCHAR(30) NOT NULL,
                    Destination NVARCHAR(255) NULL,
                    SentAt DATETIME2 NULL,
                    DeliveredAt DATETIME2 NULL,
                    Status NVARCHAR(30) NOT NULL,
                    FailureReason NVARCHAR(500) NULL);
                CREATE INDEX IX_AlertDeliveryHistory_Alert_Channel ON dbo.AlertDeliveryHistory(AlertId,Channel);
            END;

            IF OBJECT_ID('dbo.NotificationEndpoints','U') IS NULL
            BEGIN
                CREATE TABLE dbo.NotificationEndpoints(
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    Channel NVARCHAR(30) NOT NULL,
                    Address NVARCHAR(500) NOT NULL,
                    Enabled BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NULL);
                CREATE UNIQUE INDEX UX_NotificationEndpoints_User_Channel_Address ON dbo.NotificationEndpoints(UserId,Channel,Address);
            END;

            IF OBJECT_ID('dbo.PatientEvents', 'U') IS NOT NULL DROP TABLE dbo.PatientEvents;

            IF COL_LENGTH('dbo.GlucoseMeasurements', 'DeviceId') IS NULL
                ALTER TABLE dbo.GlucoseMeasurements ADD DeviceId INT NULL;

            IF COL_LENGTH('dbo.Users','TokenVersion') IS NULL
                ALTER TABLE dbo.Users ADD TokenVersion INT NOT NULL CONSTRAINT DF_Users_TokenVersion DEFAULT 0;
            IF OBJECT_ID('dbo.UserSessions','U') IS NOT NULL DROP TABLE dbo.UserSessions;
            IF OBJECT_ID('dbo.RefreshTokens','U') IS NOT NULL DROP TABLE dbo.RefreshTokens;

            IF OBJECT_ID('dbo.PasswordResetTokens','U') IS NOT NULL AND OBJECT_ID('dbo.PasswordResetRequests','U') IS NULL
                EXEC sp_rename 'dbo.PasswordResetTokens', 'PasswordResetRequests';

            IF OBJECT_ID('dbo.PasswordResetRequests','U') IS NULL
            BEGIN
                CREATE TABLE dbo.PasswordResetRequests (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NULL,
                    Email NVARCHAR(255) NOT NULL,
                    Token NVARCHAR(255) NOT NULL,
                    OtpCode NVARCHAR(128) NOT NULL,
                    ExpiresAt DATETIME2 NOT NULL,
                    IsUsed BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UsedAt DATETIME2 NULL,
                    CONSTRAINT FK_PasswordResetRequests_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE);
                CREATE INDEX IX_PasswordResetRequests_Email ON dbo.PasswordResetRequests(Email);
                CREATE INDEX IX_PasswordResetRequests_Token ON dbo.PasswordResetRequests(Token);
                CREATE INDEX IX_PasswordResetTokens_Email_Otp ON dbo.PasswordResetRequests(Email, OtpCode, IsUsed);
            END
            ELSE
            BEGIN
                IF (SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PasswordResetRequests') AND name = 'OtpCode') < 256
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordResetTokens_Email_Otp' AND object_id = OBJECT_ID('dbo.PasswordResetRequests'))
                        DROP INDEX IX_PasswordResetTokens_Email_Otp ON dbo.PasswordResetRequests;
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PasswordResetRequests_Email_Otp' AND object_id = OBJECT_ID('dbo.PasswordResetRequests'))
                        DROP INDEX IX_PasswordResetRequests_Email_Otp ON dbo.PasswordResetRequests;

                    ALTER TABLE dbo.PasswordResetRequests ALTER COLUMN OtpCode NVARCHAR(128) NOT NULL;

                    CREATE INDEX IX_PasswordResetTokens_Email_Otp ON dbo.PasswordResetRequests(Email, OtpCode, IsUsed);
                END
            END;
            """;

        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
