# Docker Staging

This setup runs AuthService in `Production` mode with PostgreSQL and production-like OpenIddict certificates.

## 1. Generate Local PFX Certificates

```bash
chmod +x deploy/generate-compose-certs.sh
./deploy/generate-compose-certs.sh
```

The generated files are placed in `deploy/certs/` and are ignored by git.

These certificates are only for local staging. Do not use them in real production.

## 2. Start Staging Stack

```bash
docker compose -f docker-compose.staging.yml up --build
```

Compose starts:

- PostgreSQL on host port `5434`
- migration container that applies EF Core migrations
- AuthService on `http://localhost:5058`
- HTTPS demo proxy on `https://localhost:8443`

## 3. Check Health

```bash
curl -i http://localhost:5058/health/live
curl -i http://localhost:5058/health/ready
```

## 4. Visual Login Demo

Open:

```text
https://localhost:8443/demo
```

The proxy uses a local self-signed TLS certificate, so the browser will show a certificate warning.
Accept it for local staging only.

Use the seeded development user:

```text
docker-admin@example.com
Admin123!Admin123!
```

## 5. Stop

```bash
docker compose -f docker-compose.staging.yml down
```

To remove the PostgreSQL data volume:

```bash
docker compose -f docker-compose.staging.yml down -v
```

## Notes

`OpenIddict__SeedOnStartup=true` is enabled for the AuthService container so local staging gets the debug clients/scopes.

For real production, keep `OpenIddict__SeedOnStartup=false` and seed clients through a controlled deployment/admin process.
