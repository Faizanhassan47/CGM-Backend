# Phases 4–6 architecture and deployment

## Deployment order

1. Back up SQL Server and apply `Database/Phase3DataFoundationMigration.sql` if it has not already been applied.
2. Apply `Database/Phase4To6Migration.sql`.
3. Configure 15-minute JWT access tokens and optional Push/SMS provider webhooks from `.env.example`.
4. Deploy the API, then the mobile application.

## Identity boundary

JWT issuance remains in `TokenService`; persistent device sessions are stored in `UserSessions`; HTTP session management is isolated in `AccountController`. Refresh tokens are rotated and hashed. Recovery tokens and OTPs are hashed, single-use, expiring, and legacy clear-text recovery rows are invalidated by migration. Login outcomes are recorded without passwords.

## Alert boundary

The API transaction evaluates `AlertRules`, prevents duration-window duplicates, creates the alert, and adds a durable `AlertQueue` row. `AlertNotificationWorker` claims batches transactionally and performs delivery outside the measurement request. Email, Push, and SMS attempts produce independent `AlertDeliveryHistory` records and retry with backoff up to three times. Push/SMS use provider-neutral authenticated webhooks.

## Application boundaries

Controllers own HTTP/authentication concerns. Token, alert-processing, delivery, auditing, and external-notification workflows live in services. EF entities and SQL configuration remain in the data/infrastructure boundary. The MAUI application separates authenticated transport, sessions, synchronization, diagnostics, CGM protocol, reports, and page view models. `ApplicationErrorHandler` is the central mobile error boundary.

Environment-specific backend settings are provided for Development, Staging, and Production. The mobile app rejects non-HTTPS API URLs when `CGM_ENVIRONMENT=Production`.
