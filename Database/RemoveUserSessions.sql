SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Users', 'TokenVersion') IS NULL
    ALTER TABLE dbo.Users ADD TokenVersion INT NOT NULL
        CONSTRAINT DF_Users_TokenVersion DEFAULT 0;

IF OBJECT_ID('dbo.UserSessions', 'U') IS NOT NULL
    DROP TABLE dbo.UserSessions;

IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NOT NULL
    DROP TABLE dbo.RefreshTokens;

COMMIT TRANSACTION;
