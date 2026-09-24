# CGM operations runbook

## Service monitoring

- Probe `GET /health` every minute. Page the on-call engineer after two consecutive `503` responses or a `degraded` queue state.
- Collect JSON stdout logs in the environment's centralized log platform. Index correlation ID, user ID, path, status, duration, and exception type; restrict log access because CGM data is sensitive.
- Scrape `GET /metrics` from the private operations network. Alert on API errors above 2% for five minutes, p95 latency above 1 second, failed notification deliveries, queue lag above ten minutes, and 20 failed logins within five minutes.
- The application emits structured warnings for failed-login spikes and errors for notification backlog/failures. Route these severities to the on-call channel in the log platform.

## SQL Server backup and recovery

- Use SQL Server `FULL` recovery mode. Run a verified full backup daily, differential backup every six hours, and transaction-log backup every 15 minutes using `Backup-CgmDatabase.ps1` or equivalent SQL Agent jobs.
- Encrypt backups, store copies in a separate region/account, restrict restore permission, and retain them according to clinical and legal policy.
- Run a restore drill monthly in an isolated environment and record achieved RPO/RTO. The operating targets are RPO 15 minutes and RTO 2 hours.
- Before database migration: take and verify a backup, run scripts against staging, review schema/data checks, approve production, then execute `Apply-Migrations.ps1 -Approved`. Keep each migration forward-compatible while old app instances drain.

## Deployment and incident response

1. CI must pass unit, integration, contract, dependency, and application-build jobs.
2. Deploy the immutable artifact to staging and complete smoke tests (`/health`, login, reading upload, alert delivery).
3. Obtain production-environment approval, deploy, and watch error/latency/queue metrics for 30 minutes.
4. If service health regresses, roll back the application artifact. Apply a compensating database migration; do not restore over live data unless disaster recovery is declared.
5. For suspected account abuse, preserve correlated logs, revoke active sessions, rotate affected secrets, and follow the incident communication policy.

Never place connection strings, tokens, patient values, passwords, or reset codes in logs or CI artifacts.
