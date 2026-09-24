# Phase 1 production hardening

## Device API security

`DevicesController` now requires JWT authentication. Device reads, updates, registration by serial number, and removal verify the authenticated user against `CGMDevices.UserId`. Existing resources owned by another user return HTTP 403; missing resources return HTTP 404. Both `PUT /api/devices/{id}` and the existing `PUT /api/devices/{id}/status` route are supported.

The existing `IX_CGMDevices_UserId` index in `CgmDbContext` supports ownership queries.

## API exception handling

`GlobalExceptionMiddleware` records the exception, HTTP method, request path, authenticated user identifier, UTC timestamp, and a generated correlation identifier. Clients receive only a stable 500 response containing `success`, a safe message, and `errorId`; exception internals are not returned.

## Regression coverage

`CGM.Api.Tests` verifies authorization is required and that cross-user device reads, updates, deletes, and serial registration are forbidden.
