CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AlertDeliveryHistory` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `AlertId` bigint NOT NULL,
    `Channel` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Destination` varchar(255) CHARACTER SET utf8mb4 NULL,
    `SentAt` datetime(6) NULL,
    `DeliveredAt` datetime(6) NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `FailureReason` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AlertDeliveryHistory` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AlertRules` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `AlertType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `MinimumValue` decimal(10,2) NULL,
    `MaximumValue` decimal(10,2) NULL,
    `DurationMinutes` int NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AlertRules` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NULL,
    `Action` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Entity` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` varchar(100) CHARACTER SET utf8mb4 NULL,
    `OldValue` longtext CHARACTER SET utf8mb4 NULL,
    `NewValue` longtext CHARACTER SET utf8mb4 NULL,
    `IpAddress` varchar(100) CHARACTER SET utf8mb4 NULL,
    `CorrelationId` varchar(100) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `DailyGlucoseSummaries` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `SummaryDate` date NOT NULL,
    `ReadingCount` int NOT NULL,
    `MeanGlucose` decimal(10,2) NOT NULL,
    `MedianGlucose` decimal(10,2) NOT NULL,
    `StandardDeviation` decimal(10,2) NOT NULL,
    `Gmi` decimal(10,2) NOT NULL,
    `CvPercentage` decimal(10,2) NOT NULL,
    `TimeInRangeMinutes` int NOT NULL,
    `TimeBelowRangeMinutes` int NOT NULL,
    `TimeAboveRangeMinutes` int NOT NULL,
    `TimeInRangePercentage` decimal(5,2) NOT NULL,
    `TimeBelowRangePercentage` decimal(5,2) NOT NULL,
    `TimeAboveRangePercentage` decimal(5,2) NOT NULL,
    `LowestGlucose` decimal(10,2) NOT NULL,
    `HighestGlucose` decimal(10,2) NOT NULL,
    `HighEventsCount` int NOT NULL,
    `LowEventsCount` int NOT NULL,
    `GeneratedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_DailyGlucoseSummaries` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `NotificationEndpoints` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `Channel` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Address` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    CONSTRAINT `PK_NotificationEndpoints` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Users` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FullName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(500) CHARACTER SET utf8mb4 NULL,
    `AuthProvider` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `GoogleSubjectId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `AppleSubjectId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `LastLoginDeviceId` varchar(255) CHARACTER SET utf8mb4 NULL,
    `LastLoginDeviceInfo` varchar(255) CHARACTER SET utf8mb4 NULL,
    `EmailVerified` tinyint(1) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `ReferralCode` varchar(12) CHARACTER SET utf8mb4 NULL,
    `LastLoginAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `TokenVersion` int NOT NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `CGMDevices` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `DeviceName` varchar(150) CHARACTER SET utf8mb4 NULL,
    `DeviceType` varchar(50) CHARACTER SET utf8mb4 NULL,
    `DeviceModel` varchar(50) CHARACTER SET utf8mb4 NULL,
    `SerialNumber` varchar(100) CHARACTER SET utf8mb4 NULL,
    `BleDeviceName` varchar(150) CHARACTER SET utf8mb4 NULL,
    `FirmwareVersion` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BatteryVoltageMv` int NULL,
    `BatteryPercentage` decimal(5,2) NULL,
    `ConnectionStatus` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `LastConnectedAt` datetime(6) NULL,
    `LastCommunicationAt` datetime(6) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    CONSTRAINT `PK_CGMDevices` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CGMDevices_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Families` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FamilyName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `OwnerUserId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `LowGlucoseThreshold` decimal(10,2) NOT NULL,
    `HighGlucoseThreshold` decimal(10,2) NOT NULL,
    CONSTRAINT `PK_Families` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Families_Users_OwnerUserId` FOREIGN KEY (`OwnerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `PasswordResetRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NULL,
    `Email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Token` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `OtpCode` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `IsUsed` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6) NULL,
    CONSTRAINT `PK_PasswordResetRequests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PasswordResetRequests_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `PatientProfile` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `PhoneNumber` varchar(30) CHARACTER SET utf8mb4 NULL,
    `DateOfBirth` date NULL,
    `Gender` varchar(30) CHARACTER SET utf8mb4 NULL,
    `PreferredGlucoseUnit` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `Language` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Theme` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ProfileCompleted` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    CONSTRAINT `PK_PatientProfile` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PatientProfile_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Sensors` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `DeviceId` int NOT NULL,
    `SensorIdentifier` varchar(150) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `StartedAt` datetime(6) NULL,
    `ActivatedAt` datetime(6) NULL,
    `LastReadingAt` datetime(6) NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Sensors` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Sensors_CGMDevices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `CGMDevices` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Sensors_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `FamilyMembers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FamilyId` int NOT NULL,
    `UserId` int NULL,
    `MemberEmail` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `JoinedByReferralCode` varchar(12) CHARACTER SET utf8mb4 NULL,
    `Role` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ReceiveAlerts` tinyint(1) NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `JoinedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_FamilyMembers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FamilyMembers_Families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES `Families` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_FamilyMembers_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Alerts` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `SensorId` int NULL,
    `AlertType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Message` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `GlucoseValue` decimal(10,2) NULL,
    `GlucoseUnit` varchar(10) CHARACTER SET utf8mb4 NULL,
    `Severity` varchar(20) CHARACTER SET utf8mb4 NULL,
    `IsRead` tinyint(1) NOT NULL,
    `AlertTime` datetime(6) NOT NULL,
    `ReadAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Alerts` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Alerts_Sensors_SensorId` FOREIGN KEY (`SensorId`) REFERENCES `Sensors` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_Alerts_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `GlucoseMeasurements` (
    `SensorId` int NOT NULL,
    `MeasurementTime` datetime(6) NOT NULL,
    `UserId` int NOT NULL,
    `DeviceId` int NULL,
    `GlucoseValue` decimal(10,2) NULL,
    `BatteryVoltageMv` int NULL,
    `DeviceTemperatureC` decimal(10,2) NULL,
    `WE1CurrentNa` decimal(12,4) NULL,
    `IsSynced` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_GlucoseMeasurements` PRIMARY KEY (`SensorId`, `MeasurementTime`),
    CONSTRAINT `FK_GlucoseMeasurements_CGMDevices_DeviceId` FOREIGN KEY (`DeviceId`) REFERENCES `CGMDevices` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_GlucoseMeasurements_Sensors_SensorId` FOREIGN KEY (`SensorId`) REFERENCES `Sensors` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_GlucoseMeasurements_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `AlertHistory` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `AlertId` bigint NOT NULL,
    `UserId` int NOT NULL,
    `AlertType` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Threshold` decimal(10,2) NULL,
    `TriggeredAt` datetime(6) NOT NULL,
    `DeliveredAt` datetime(6) NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AlertHistory` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AlertHistory_Alerts_AlertId` FOREIGN KEY (`AlertId`) REFERENCES `Alerts` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AlertQueue` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `AlertId` bigint NOT NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `RetryCount` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `NextAttemptAt` datetime(6) NULL,
    `ProcessedAt` datetime(6) NULL,
    `ErrorMessage` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AlertQueue` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AlertQueue_Alerts_AlertId` FOREIGN KEY (`AlertId`) REFERENCES `Alerts` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AlertRecipients` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `AlertId` bigint NOT NULL,
    `UserId` int NOT NULL,
    `IsRead` tinyint(1) NOT NULL,
    `ReadAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AlertRecipients` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AlertRecipients_Alerts_AlertId` FOREIGN KEY (`AlertId`) REFERENCES `Alerts` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AlertRecipients_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_AlertDeliveryHistory_Alert_Channel` ON `AlertDeliveryHistory` (`AlertId`, `Channel`);

CREATE INDEX `IX_AlertHistory_AlertId` ON `AlertHistory` (`AlertId`);

CREATE INDEX `IX_AlertHistory_User_TriggeredAt` ON `AlertHistory` (`UserId`, `TriggeredAt`);

CREATE INDEX `IX_AlertQueue_AlertId` ON `AlertQueue` (`AlertId`);

CREATE INDEX `IX_AlertQueue_Status_NextAttempt` ON `AlertQueue` (`Status`, `NextAttemptAt`);

CREATE INDEX `IX_AlertRecipients_User_Read` ON `AlertRecipients` (`UserId`, `IsRead`);

CREATE UNIQUE INDEX `UX_AlertRecipients_Alert_User` ON `AlertRecipients` (`AlertId`, `UserId`);

CREATE INDEX `IX_AlertRules_User_Type_Enabled` ON `AlertRules` (`UserId`, `AlertType`, `Enabled`);

CREATE INDEX `IX_Alerts_SensorId` ON `Alerts` (`SensorId`);

CREATE INDEX `IX_Alerts_User_Read` ON `Alerts` (`UserId`, `IsRead`);

CREATE INDEX `IX_Alerts_User_Time` ON `Alerts` (`UserId`, `AlertTime`);

CREATE INDEX `IX_AuditLogs_Entity_EntityId` ON `AuditLogs` (`Entity`, `EntityId`);

CREATE INDEX `IX_AuditLogs_User_CreatedAt` ON `AuditLogs` (`UserId`, `CreatedAt`);

CREATE INDEX `IX_CGMDevices_UserId` ON `CGMDevices` (`UserId`);

CREATE UNIQUE INDEX `UX_CGMDevices_SerialNumber` ON `CGMDevices` (`SerialNumber`);

CREATE UNIQUE INDEX `UX_DailyGlucoseSummaries_User_Date` ON `DailyGlucoseSummaries` (`UserId`, `SummaryDate`);

CREATE UNIQUE INDEX `UX_Families_ActiveOwner` ON `Families` (`OwnerUserId`);

CREATE INDEX `IX_FamilyMembers_JoinedByReferralCode` ON `FamilyMembers` (`JoinedByReferralCode`);

CREATE INDEX `IX_FamilyMembers_UserId` ON `FamilyMembers` (`UserId`);

CREATE UNIQUE INDEX `UX_FamilyMembers_Family_User` ON `FamilyMembers` (`FamilyId`, `UserId`);

CREATE INDEX `IX_GlucoseMeasurements_Device_Time` ON `GlucoseMeasurements` (`DeviceId`, `MeasurementTime`);

CREATE INDEX `IX_GlucoseMeasurements_User_Time` ON `GlucoseMeasurements` (`UserId`, `MeasurementTime`);

CREATE UNIQUE INDEX `UX_NotificationEndpoints_User_Channel_Address` ON `NotificationEndpoints` (`UserId`, `Channel`, `Address`);

CREATE INDEX `IX_PasswordResetRequests_UserId` ON `PasswordResetRequests` (`UserId`);

CREATE INDEX `IX_PasswordResetTokens_Email` ON `PasswordResetRequests` (`Email`);

CREATE INDEX `IX_PasswordResetTokens_Email_Otp` ON `PasswordResetRequests` (`Email`, `OtpCode`, `IsUsed`);

CREATE INDEX `IX_PasswordResetTokens_Token` ON `PasswordResetRequests` (`Token`);

CREATE UNIQUE INDEX `UX_PatientProfile_UserId` ON `PatientProfile` (`UserId`);

CREATE INDEX `IX_Sensors_DeviceId` ON `Sensors` (`DeviceId`);

CREATE INDEX `IX_Sensors_UserId` ON `Sensors` (`UserId`);

CREATE UNIQUE INDEX `UX_Users_AppleSubjectId` ON `Users` (`AppleSubjectId`);

CREATE UNIQUE INDEX `UX_Users_Email` ON `Users` (`Email`);

CREATE UNIQUE INDEX `UX_Users_GoogleSubjectId` ON `Users` (`GoogleSubjectId`);

CREATE UNIQUE INDEX `UX_Users_ReferralCode` ON `Users` (`ReferralCode`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260925074117_InitialMySql', '9.0.0');

COMMIT;

