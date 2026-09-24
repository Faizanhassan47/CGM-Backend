SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Users','TokenVersion') IS NULL
    ALTER TABLE dbo.Users ADD TokenVersion INT NOT NULL CONSTRAINT DF_Users_TokenVersion DEFAULT 0;
IF OBJECT_ID('dbo.UserSessions','U') IS NOT NULL DROP TABLE dbo.UserSessions;
IF OBJECT_ID('dbo.RefreshTokens','U') IS NOT NULL DROP TABLE dbo.RefreshTokens;

IF OBJECT_ID('dbo.LoginHistory','U') IS NOT NULL DROP TABLE dbo.LoginHistory;

IF OBJECT_ID('dbo.PasswordResetTokens','U') IS NOT NULL AND OBJECT_ID('dbo.PasswordResetRequests','U') IS NULL
    EXEC sp_rename 'dbo.PasswordResetTokens', 'PasswordResetRequests';
-- Existing rows contain legacy clear-text recovery values and must never remain valid.
IF OBJECT_ID('dbo.PasswordResetRequests','U') IS NOT NULL
    UPDATE dbo.PasswordResetRequests SET IsUsed=1,UsedAt=COALESCE(UsedAt,SYSUTCDATETIME()) WHERE IsUsed=0;

IF OBJECT_ID('dbo.AlertRules','U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertRules(Id BIGINT IDENTITY PRIMARY KEY,UserId INT NOT NULL,AlertType NVARCHAR(50) NOT NULL,
      MinimumValue DECIMAL(10,2) NULL,MaximumValue DECIMAL(10,2) NULL,DurationMinutes INT NOT NULL DEFAULT 15,
      Enabled BIT NOT NULL DEFAULT 1,CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
    CREATE INDEX IX_AlertRules_User_Type_Enabled ON dbo.AlertRules(UserId,AlertType,Enabled);
END;
IF OBJECT_ID('dbo.AlertQueue','U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertQueue(Id BIGINT IDENTITY PRIMARY KEY,AlertId BIGINT NOT NULL,Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
      RetryCount INT NOT NULL DEFAULT 0,CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),NextAttemptAt DATETIME2 NULL,
      ProcessedAt DATETIME2 NULL,ErrorMessage NVARCHAR(500) NULL,
      CONSTRAINT FK_AlertQueue_Alert FOREIGN KEY(AlertId) REFERENCES dbo.Alerts(Id) ON DELETE CASCADE);
    CREATE INDEX IX_AlertQueue_Status_NextAttempt ON dbo.AlertQueue(Status,NextAttemptAt);
END;
IF OBJECT_ID('dbo.AlertDeliveryHistory','U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertDeliveryHistory(Id BIGINT IDENTITY PRIMARY KEY,AlertId BIGINT NOT NULL,Channel NVARCHAR(30) NOT NULL,
      Destination NVARCHAR(255) NULL,SentAt DATETIME2 NULL,DeliveredAt DATETIME2 NULL,Status NVARCHAR(30) NOT NULL,
      FailureReason NVARCHAR(500) NULL);
    CREATE INDEX IX_AlertDeliveryHistory_Alert_Channel ON dbo.AlertDeliveryHistory(AlertId,Channel);
END;
IF OBJECT_ID('dbo.NotificationEndpoints','U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationEndpoints(Id BIGINT IDENTITY PRIMARY KEY,UserId INT NOT NULL,Channel NVARCHAR(30) NOT NULL,
      Address NVARCHAR(500) NOT NULL,Enabled BIT NOT NULL DEFAULT 1,CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAt DATETIME2 NULL);
    CREATE UNIQUE INDEX UX_NotificationEndpoints_User_Channel_Address ON dbo.NotificationEndpoints(UserId,Channel,Address);
END;

COMMIT;
GO
