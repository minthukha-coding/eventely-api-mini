# Eventely API

A feature-folder ASP.NET Core 8 Web API starter for the Eventely mobile app. It uses EF Core with Neon PostgreSQL, JWT bearer authentication, Google ID-token validation, SMTP email verification, and Cloudinary image storage.

## Project structure

```text
EventelyApiMini/
├── EventelyApiMini.sln
├── README.md
└── src/
    └── Eventely.Api/
        ├── Common/                  # Problem responses, API exceptions, current-user helper
        ├── Domain/
        │   ├── Constants/           # Role names
        │   ├── Entities/            # EF Core entities
        │   └── Enums/               # Gender
        ├── Features/
        │   ├── Auth/                # DTOs, controller, JWT/OTP/Google service
        │   ├── Events/              # DTOs, controller, CRUD and participation service
        │   └── Profile/             # DTOs, controller, profile service
        ├── Infrastructure/
        │   ├── Configuration/       # Strongly named configuration options
        │   ├── Email/                # SMTP sender abstraction and implementation
        │   ├── Images/               # Image storage abstraction, validation, Cloudinary
        │   └── Persistence/          # DbContext and token cleanup worker
        ├── appsettings.json
        ├── Eventely.Api.csproj
        └── Program.cs
```

The model also includes storage public IDs for Cloudinary cleanup and a `RevokedToken` table so logout takes effect immediately. OTP `Code` stores a password-hasher value, not the email's six-digit code.

## Configure and run

The checked-in settings file contains placeholders. Set secrets outside source control, for example with .NET user-secrets:

```powershell
dotnet user-secrets init --project src/Eventely.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=YOUR-NEON-HOST;Port=5432;Database=neondb;Username=YOUR-USER;Password=YOUR-PASSWORD;SSL Mode=Require;Trust Server Certificate=true" --project src/Eventely.Api
dotnet user-secrets set "Jwt:SigningKey" "REPLACE_WITH_A_RANDOM_SECRET_AT_LEAST_32_BYTES_LONG" --project src/Eventely.Api
dotnet user-secrets set "Cloudinary:CloudName" "YOUR-CLOUD-NAME" --project src/Eventely.Api
dotnet user-secrets set "Cloudinary:ApiKey" "YOUR-API-KEY" --project src/Eventely.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "YOUR-API-SECRET" --project src/Eventely.Api
dotnet user-secrets set "Email:Host" "YOUR-SMTP-HOST" --project src/Eventely.Api
dotnet user-secrets set "Email:Username" "YOUR-SMTP-USERNAME" --project src/Eventely.Api
dotnet user-secrets set "Email:Password" "YOUR-SMTP-PASSWORD" --project src/Eventely.Api
dotnet user-secrets set "Email:FromEmail" "no-reply@example.com" --project src/Eventely.Api
dotnet user-secrets set "Google:ClientId" "YOUR-GOOGLE-OAUTH-CLIENT-ID" --project src/Eventely.Api
```

Generate a signing key with a cryptographically secure random generator; keep the same key across instances and deployments that must validate the same tokens. Use environment variables such as `Jwt__SigningKey` and a secret manager in deployed environments. Do not commit production credentials.

`Cors:AllowedOrigins` is for browser-based clients and should contain the exact web origins that need access. Native iOS/Android HTTP clients do not use browser CORS enforcement. Add any local web development origin to this array as needed.

Install EF tooling, create the initial schema, and start the API:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.31
dotnet ef migrations add InitialCreate --project src/Eventely.Api --startup-project src/Eventely.Api
dotnet ef database update --project src/Eventely.Api --startup-project src/Eventely.Api
dotnet run --project src/Eventely.Api
```

Swagger UI is available at `/swagger` in the Development environment. The API does not apply migrations automatically at startup.

## API flow

1. `POST /api/auth/register` with `{ "email": "...", "password": "..." }` creates a `NormalUser` and emails a 6-digit OTP.
2. `POST /api/auth/verify-otp` with `{ "email": "...", "code": "123456" }` confirms the address and returns a JWT.
3. `POST /api/auth/login` returns a JWT after email verification. `POST /api/auth/google` accepts a Google ID token, validates the signature and configured audience, and signs in or creates a verified `NormalUser`.
4. Use the token as `Authorization: Bearer <accessToken>`. The auth response has `requiresProfileCompletion`; finish setup with `PUT /api/profile` as `multipart/form-data` with `FirstName`, `LastName`, `Address`, `Gender` (`Male`, `Female`, or `Other`), and `ProfilePicture`.
5. `GET /api/profile/me` returns the authenticated profile.

Auth endpoints are rate-limited to five requests per minute per client IP. OTPs expire after the configured `Otp:ExpirationMinutes` (10 by default), expire when a replacement code is issued, and can only be used once. The registration SMTP send occurs before its database transaction commits, so a delivery failure rolls back the new account and can be retried. In a larger deployment, move delivery to a durable email outbox/worker.

### Roles

Public registration and Google sign-up always assign `NormalUser`; the API never accepts a client-supplied role. Assign `EventCreator` through trusted admin tooling, for example:

```sql
UPDATE application_users SET "Role" = 'EventCreator' WHERE "Email" = 'creator@example.com';
```

Do not expose that SQL as a public endpoint. Newly issued JWTs contain `userId`, `email`, `role`, `sub`, and `jti`. Role authorization restricts event creation and service ownership checks restrict event updates/deletes to their creator. Both roles can join and leave events.

`POST /api/auth/logout` revokes the presented token server-side and returns `204`; the mobile app should also delete its locally stored token. Revocation is checked against PostgreSQL on authenticated requests, and expired revocation rows are removed by a hosted cleanup worker. Access tokens last 15 minutes by default. This mini project does not issue refresh tokens.

### Events

- `GET /api/events?page=1&pageSize=20` lists events with creator details, detail-image URLs, and participant counts.
- `GET /api/events/{id}` returns one event.
- `POST /api/events` creates an event; send multipart fields `Title`, `Description`, `Location`, `EventDateTime`, `CoverImage` (required), and repeated `DetailImages` (up to 10).
- `PUT /api/events/{id}` updates fields and optionally replaces `CoverImage`. Detail images remain as-is unless `ReplaceDetailImages=true`; with that flag and no files, the existing detail-image list is cleared.
- `DELETE /api/events/{id}` deletes the event and its Cloudinary images.
- `POST /api/events/{id}/join` and `DELETE /api/events/{id}/join` join/leave idempotently.

Profile photos and event images accept JPEG, PNG, or WebP up to 5 MB each. The API checks file signatures, not only the client-provided MIME type. Each upload stores its Cloudinary public ID alongside the URL to support cleanup/replacement.

## Configuration reference

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Neon PostgreSQL connection string; use SSL. |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, `Jwt:AccessTokenMinutes` | JWT validation/issuance settings; signing key must be at least 32 UTF-8 bytes. |
| `Google:ClientId` | Expected audience for Google ID tokens. |
| `Cloudinary:CloudName`, `ApiKey`, `ApiSecret` | Cloudinary server-side upload and delete credentials. |
| `Email:Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`, `EnableSsl` | SMTP verification mail settings. |
| `Otp:ExpirationMinutes` | OTP lifetime, clamped to 1–30 minutes. |
| `Cors:AllowedOrigins` | Browser origins allowed by the named CORS policy. |

## Status and operational notes

- Validation errors use ASP.NET Core problem responses. Service errors are returned as RFC 7807-style `ProblemDetails`; unexpected failures are logged and returned without internal exception details.
- Event writes span PostgreSQL and Cloudinary, which cannot share a transaction. The code cleans up newly uploaded files if a database write fails and logs cleanup failures after successful database writes. Run periodic Cloudinary orphan reconciliation for high-volume production use.
- Configure trusted proxy/forwarded-header handling before relying on IP-based rate limiting behind a reverse proxy or load balancer.
- The NuGet package versions are pinned in the project file for repeatable restores.
