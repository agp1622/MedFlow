# Research

- **Scheduling**: `BackgroundService` with `PeriodicTimer`, creating a scope per pass. Chosen over Hangfire/Quartz (new dependency, constitution V). Limitation: single instance; two instances could double-send (documented).
- **Email**: reuse `IEmailSender`; same pattern as `PortalInvitationsController` for link building from `AllowedOrigins[0]`.
- **Token**: 32 random bytes, URL-safe base64, SHA-256 hex stored (same approach as `PortalInvitation`).
- **GET vs POST for actions**: link opens a client page (safe, no state change); email scanners prefetching the URL cannot confirm/cancel. API uses POST.
- **SMS**: out of scope, no provider.
- **Time**: all comparisons UTC like existing code.
