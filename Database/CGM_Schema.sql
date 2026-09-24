conect youUSE CGM;
GO

/* =========================================================
   1. USERS
   Login / Signup / Google / Apple account information
   ========================================================= */

CREATE TABLE dbo.Users
(
    Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    FullName            NVARCHAR(150) NOT NULL,
    Email               NVARCHAR(255) NOT NULL,

    -- Null for Google/Apple-only users
    PasswordHash        NVARCHAR(500) NULL,

    -- Email / Google / Apple
    AuthProvider        NVARCHAR(30) NOT NULL DEFAULT 'Email',

    -- Google's unique user identifier
    GoogleSubjectId     NVARCHAR(255) NULL,

    -- Apple's unique user identifier
    AppleSubjectId      NVARCHAR(255) NULL,

    LastLoginDeviceId   NVARCHAR(255) NULL,
    LastLoginDeviceInfo NVARCHAR(255) NULL,

    EmailVerified       BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,

    LastLoginAt         DATETIME2 NULL,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NULL,
    TokenVersion        INT NOT NULL DEFAULT 0
);
GO


/* Email should be unique */
CREATE UNIQUE INDEX UX_Users_Email
ON dbo.Users(Email);
GO


/* Google ID should be unique when present */
CREATE UNIQUE INDEX UX_Users_GoogleSubjectId
ON dbo.Users(GoogleSubjectId)
WHERE GoogleSubjectId IS NOT NULL;
GO


/* Apple ID should be unique when present */
CREATE UNIQUE INDEX UX_Users_AppleSubjectId
ON dbo.Users(AppleSubjectId)
WHERE AppleSubjectId IS NOT NULL;
GO



/* =========================================================
   2. PATIENT PROFILE
   Additional patient/user information
   ========================================================= */

CREATE TABLE dbo.PatientProfile
(
    Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId              INT NOT NULL,

    PhoneNumber         NVARCHAR(30) NULL,
    DateOfBirth         DATE NULL,

    Gender              NVARCHAR(30) NULL,

    -- mg/dL or mmol/L
    PreferredGlucoseUnit NVARCHAR(10) NOT NULL DEFAULT 'mg/dL',

    Language            NVARCHAR(20) NOT NULL DEFAULT 'English',

    -- System / Light / Dark
    Theme               NVARCHAR(20) NOT NULL DEFAULT 'System',

    ProfileCompleted    BIT NOT NULL DEFAULT 0,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NULL,

    CONSTRAINT FK_PatientProfile_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id)
);
GO


/* One profile per user */
CREATE UNIQUE INDEX UX_PatientProfile_UserId
ON dbo.PatientProfile(UserId);
GO



/* =========================================================
   3. CGM DEVICES
   Physical CGM hardware connected with BLE
   ========================================================= */

CREATE TABLE dbo.CGMDevices
(
    Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId              INT NOT NULL,

    DeviceName          NVARCHAR(150) NULL,

    -- Example:
    -- Disposable
    -- Reusable
    DeviceType          NVARCHAR(50) NULL,

    -- Example:
    -- G-AA
    -- G-PA0
    -- G-PA1
    DeviceModel         NVARCHAR(50) NULL,

    -- E8 result
    SerialNumber        NVARCHAR(100) NULL,

    -- Example:
    -- CGM-0078A3E3C221
    BleDeviceName       NVARCHAR(150) NULL,

    -- E7 result, example A100
    FirmwareVersion     NVARCHAR(50) NULL,

    -- E1 result
    BatteryVoltageMv    INT NULL,

    -- Don't calculate percentage until vendor provides mapping
    BatteryPercentage   DECIMAL(5,2) NULL,

    -- Connected / Disconnected / Inactive
    ConnectionStatus    NVARCHAR(30) NOT NULL DEFAULT 'Disconnected',

    LastConnectedAt     DATETIME2 NULL,
    LastCommunicationAt DATETIME2 NULL,

    IsActive            BIT NOT NULL DEFAULT 1,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2 NULL,

    CONSTRAINT FK_CGMDevices_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id)
);
GO


CREATE INDEX IX_CGMDevices_UserId
ON dbo.CGMDevices(UserId);
GO


CREATE UNIQUE INDEX UX_CGMDevices_SerialNumber
ON dbo.CGMDevices(SerialNumber)
WHERE SerialNumber IS NOT NULL;
GO



/* =========================================================
   4. SENSORS
   Each CGM sensor/session
   ========================================================= */

CREATE TABLE dbo.Sensors
(
    Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId              INT NOT NULL,
    DeviceId            INT NOT NULL,

    SensorIdentifier    NVARCHAR(150) NULL,

    -- Active / Warmup / EndingSoon / Expired / Inactive
    Status              NVARCHAR(30) NOT NULL DEFAULT 'Inactive',

    StartedAt           DATETIME2 NULL,
    ActivatedAt         DATETIME2 NULL,

    LastReadingAt       DATETIME2 NULL,

    -- Latest measurement SN received from this sensor
    LatestSequenceNumber INT NULL,

    IsActive            BIT NOT NULL DEFAULT 1,

    CONSTRAINT FK_Sensors_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id),

    CONSTRAINT FK_Sensors_Device
        FOREIGN KEY (DeviceId)
        REFERENCES dbo.CGMDevices(Id)
);
GO


CREATE INDEX IX_Sensors_UserId
ON dbo.Sensors(UserId);
GO

CREATE INDEX IX_Sensors_DeviceId
ON dbo.Sensors(DeviceId);
GO



/* =========================================================
   5. GLUCOSE MEASUREMENTS
   Actual CGM readings
   ========================================================= */

CREATE TABLE dbo.GlucoseMeasurements
(
    UserId              INT NOT NULL,
    SensorId            INT NOT NULL,

    -- Final glucose value
    GlucoseValue        DECIMAL(10,2) NULL,

    MeasurementTime     DATETIME2 NOT NULL,

    /* Raw CGM values.
       Useful for diagnostic/R&D purposes.
       Do NOT treat WE1 as final glucose.
    */

    BatteryVoltageMv    INT NULL,

    DeviceTemperatureC  DECIMAL(10,2) NULL,

    WE1CurrentNa        DECIMAL(12,4) NULL,

    IsSynced            BIT NOT NULL DEFAULT 0,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_GlucoseMeasurements_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id),

    CONSTRAINT FK_GlucoseMeasurements_Sensor
        FOREIGN KEY (SensorId)
        REFERENCES dbo.Sensors(Id),

    CONSTRAINT PK_GlucoseMeasurements PRIMARY KEY (SensorId, MeasurementTime)
);
GO



/* Very important for graphs/history */
CREATE INDEX IX_GlucoseMeasurements_User_Time
ON dbo.GlucoseMeasurements(UserId, MeasurementTime DESC);
GO




/* =========================================================
   6. ALERTS
   Glucose / Device / Sensor alerts
   ========================================================= */

CREATE TABLE dbo.Alerts
(
    Id                  BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId              INT NOT NULL,

    SensorId            INT NULL,

    -- LowGlucose
    -- HighGlucose
    -- RapidRise
    -- RapidFall
    -- NoData
    -- SensorEnding
    -- SensorExpired
    -- BatteryLow
    -- ConnectionLost
    AlertType           NVARCHAR(50) NOT NULL,

    Title               NVARCHAR(200) NOT NULL,
    Message             NVARCHAR(1000) NULL,

    GlucoseValue        DECIMAL(10,2) NULL,
    GlucoseUnit         NVARCHAR(10) NULL,

    -- Info / Warning / Critical
    Severity            NVARCHAR(20) NULL,

    IsRead              BIT NOT NULL DEFAULT 0,

    AlertTime           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    ReadAt              DATETIME2 NULL,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Alerts_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id),

    CONSTRAINT FK_Alerts_Sensor
        FOREIGN KEY (SensorId)
        REFERENCES dbo.Sensors(Id)
);
GO


CREATE INDEX IX_Alerts_User_Time
ON dbo.Alerts(UserId, AlertTime DESC);
GO


CREATE INDEX IX_Alerts_User_Read
ON dbo.Alerts(UserId, IsRead);
GO



/* =========================================================
   8. PASSWORD RESET TOKENS
   15-minute expiration reset tokens & OTP verification
   ========================================================= */

CREATE TABLE dbo.PasswordResetTokens
(
    Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,

    UserId              INT NULL,
    Email               NVARCHAR(255) NOT NULL,
    Token               NVARCHAR(255) NOT NULL,
    OtpCode             NVARCHAR(10) NOT NULL,

    ExpiresAt           DATETIME2 NOT NULL,
    IsUsed              BIT NOT NULL DEFAULT 0,

    CreatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UsedAt              DATETIME2 NULL,

    CONSTRAINT FK_PasswordResetTokens_User
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(Id)
        ON DELETE CASCADE
);
GO

CREATE INDEX IX_PasswordResetTokens_Email ON dbo.PasswordResetTokens(Email);
GO

CREATE INDEX IX_PasswordResetTokens_Token ON dbo.PasswordResetTokens(Token);
GO

CREATE INDEX IX_PasswordResetTokens_Email_Otp ON dbo.PasswordResetTokens(Email, OtpCode, IsUsed);
GO


/* =========================================================
   9. TEST SEED DATA
   ========================================================= */

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'faizanhassan47@gmail.com')
BEGIN
    INSERT INTO dbo.Users (FullName, Email, PasswordHash, AuthProvider, EmailVerified, IsActive, CreatedAt)
    VALUES ('Faizan Hassan', 'faizanhassan47@gmail.com', 'A+SaltGenerated.Pbkdf2HashValue123456789', 'Email', 1, 1, SYSUTCDATETIME());

    DECLARE @NewUserId INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientProfile (UserId, PreferredGlucoseUnit, Language, Theme, ProfileCompleted, CreatedAt)
    VALUES (@NewUserId, 'mg/dL', 'English', 'System', 1, SYSUTCDATETIME());
END;
GO

