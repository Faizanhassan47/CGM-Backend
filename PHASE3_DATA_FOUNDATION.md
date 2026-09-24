# Phase 3 SQL Server data foundation

Apply `Database/Phase3DataFoundationMigration.sql` to each SQL Server environment before deploying the updated API.

The migration is idempotent and adds direct device/source attribution to glucose measurements, a device/time history index, `AuditLogs`, `AlertHistory`, and the separate `reporting.DailyGlucoseSummaries` table. The reporting refresh procedure aggregates a requested UTC day without running dashboard queries over the live measurement table.

EF save interception records inserts, updates, and deletes with user, entity, old/new values, IP address, correlation ID, and timestamp. Password, token, OTP, and verification-code fields are redacted. Alert generation records threshold, trigger time, delivery time, and delivery status.

The migration creates annual SQL Server partition function/scheme objects but deliberately does not move the production table online. A DBA must validate SQL Server edition, filegroups, backups, query plans, and the maintenance window before rebuilding the clustered index onto the partition scheme.
