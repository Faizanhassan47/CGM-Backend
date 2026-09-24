SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.GlucoseMeasurements', 'DeviceId') IS NULL
    ALTER TABLE dbo.GlucoseMeasurements ADD DeviceId INT NULL;
IF COL_LENGTH('dbo.GlucoseMeasurements', 'Source') IS NULL
    ALTER TABLE dbo.GlucoseMeasurements ADD Source NVARCHAR(30) NOT NULL
        CONSTRAINT DF_GlucoseMeasurements_Source DEFAULT 'CGM';

UPDATE measurement
SET DeviceId = sensor.DeviceId
FROM dbo.GlucoseMeasurements measurement
JOIN dbo.Sensors sensor ON sensor.Id = measurement.SensorId
WHERE measurement.DeviceId IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GlucoseMeasurements_Device')
    ALTER TABLE dbo.GlucoseMeasurements ADD CONSTRAINT FK_GlucoseMeasurements_Device
        FOREIGN KEY (DeviceId) REFERENCES dbo.CGMDevices(Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GlucoseMeasurements_Device_Time' AND object_id = OBJECT_ID('dbo.GlucoseMeasurements'))
    CREATE INDEX IX_GlucoseMeasurements_Device_Time
        ON dbo.GlucoseMeasurements(DeviceId, MeasurementTime DESC)
        INCLUDE (GlucoseValue, GlucoseUnit, Trend, GlucoseStatus);

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
        AverageGlucose DECIMAL(10,2) NOT NULL,
        MinimumGlucose DECIMAL(10,2) NOT NULL,
        MaximumGlucose DECIMAL(10,2) NOT NULL,
        TimeInRangePercentage DECIMAL(5,2) NOT NULL,
        RefreshedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
    CREATE UNIQUE INDEX UX_DailyGlucoseSummaries_User_Date
        ON reporting.DailyGlucoseSummaries(UserId, SummaryDate);
END;

EXEC(N'
CREATE OR ALTER PROCEDURE reporting.RefreshDailyGlucoseSummary
    @SummaryDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    MERGE reporting.DailyGlucoseSummaries AS target
    USING (
        SELECT UserId, CAST(MeasurementTime AS DATE) SummaryDate, COUNT(*) ReadingCount,
               AVG(GlucoseValue) AverageGlucose, MIN(GlucoseValue) MinimumGlucose,
               MAX(GlucoseValue) MaximumGlucose,
               CAST(100.0 * SUM(CASE WHEN GlucoseValue BETWEEN 70 AND 180 THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,2)) TimeInRangePercentage
        FROM dbo.GlucoseMeasurements
        WHERE MeasurementTime >= @SummaryDate AND MeasurementTime < DATEADD(DAY, 1, @SummaryDate)
          AND GlucoseValue IS NOT NULL
        GROUP BY UserId, CAST(MeasurementTime AS DATE)
    ) source
    ON target.UserId = source.UserId AND target.SummaryDate = source.SummaryDate
    WHEN MATCHED THEN UPDATE SET ReadingCount=source.ReadingCount, AverageGlucose=source.AverageGlucose,
        MinimumGlucose=source.MinimumGlucose, MaximumGlucose=source.MaximumGlucose,
        TimeInRangePercentage=source.TimeInRangePercentage, RefreshedAt=SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (UserId,SummaryDate,ReadingCount,AverageGlucose,MinimumGlucose,MaximumGlucose,TimeInRangePercentage)
        VALUES (source.UserId,source.SummaryDate,source.ReadingCount,source.AverageGlucose,source.MinimumGlucose,source.MaximumGlucose,source.TimeInRangePercentage);
END');

COMMIT;
GO

/* Partitioning preparation. These objects do not move the live table. Moving the
   clustered index must be scheduled as a separate DBA operation after validating
   SQL Server edition, storage layout, backup strategy, and maintenance windows. */
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_GlucoseMeasurementYear')
    CREATE PARTITION FUNCTION pf_GlucoseMeasurementYear (DATETIME2)
    AS RANGE RIGHT FOR VALUES ('2027-01-01', '2028-01-01', '2029-01-01', '2030-01-01');
GO
IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_GlucoseMeasurementYear')
    CREATE PARTITION SCHEME ps_GlucoseMeasurementYear
    AS PARTITION pf_GlucoseMeasurementYear ALL TO ([PRIMARY]);
GO
