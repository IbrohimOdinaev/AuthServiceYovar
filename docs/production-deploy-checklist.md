# AuthService Production Configuration

This file lists the minimum configuration required before running AuthService in Production.

## Required Environment Variables

Set these values on the server, container platform, or secret store:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
ConnectionStrings__AuthDb="Host=...;Port=5432;Database=auth_service;Username=...;Password=...;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100"
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://admin.example.com
OpenIddict__SeedOnStartup=false
OpenIddict__Certificates__SigningCertificatePath=/run/secrets/authservice-signing.pfx
OpenIddict__Certificates__SigningCertificatePassword=...
OpenIddict__Certificates__EncryptionCertificatePath=/run/secrets/authservice-encryption.pfx
OpenIddict__Certificates__EncryptionCertificatePassword=...
```

Do not commit real passwords, connection strings, or PFX certificates.

## Database

Run migrations before starting production traffic:

```bash
dotnet ef database update \
  --project src/AuthService.Infrastructure \
  --startup-project src/AuthService.Api
```

The production database must contain:

- Identity tables
- OpenIddict tables
- `user_sessions`
- `audit_events`
- `login_attempts`
- `DataProtectionKeys`

## Certificates

Production must not use development OpenIddict certificates.

Use two PFX certificates:

- signing certificate: signs tokens
- encryption certificate: encrypts protected OpenIddict payloads

Keep certificate files outside the repository. Mount them as deployment secrets.

## CORS

`Cors:AllowedOrigins` must contain explicit trusted origins.

Never use:

```text
*
```

with credentialed browser flows.

## Cleanup Worker

`Maintenance__Retention__Enabled=false` is the safe default for API instances.

If AuthService runs multiple API instances, enable cleanup on only one dedicated worker instance:

```bash
Maintenance__Retention__Enabled=true
```

This avoids multiple instances deleting the same old rows at the same time.

## Reverse Proxy

Run AuthService behind HTTPS on Nginx, a load balancer, or ingress.

The proxy must forward:

- `X-Forwarded-For`
- `X-Forwarded-Proto`
- `X-Forwarded-Host`

AuthService already reads these headers via `UseForwardedHeaders()`.

## Health Checks

Use these endpoints:

```text
/health/live
/health/ready
```

`/health/live` checks that the process is alive.

`/health/ready` checks that AuthService can reach PostgreSQL.

## Deployment Order

1. Create PostgreSQL database and user.
2. Apply EF Core migrations.
3. Mount OpenIddict PFX certificates.
4. Set required environment variables.
5. Start AuthService.
6. Check `/health/ready`.
7. Send traffic through the load balancer.
