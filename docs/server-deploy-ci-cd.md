# Server Deploy And CI/CD

This guide describes the simplest VPS-style production deployment:

- GitHub Actions builds and tests the code.
- GitHub Actions publishes a Docker image to GitHub Container Registry.
- GitHub Actions connects to the server over SSH.
- The server runs `docker-compose.production.yml`.
- A host-level reverse proxy such as Nginx/Caddy terminates HTTPS and forwards to `127.0.0.1:8080`.

## 1. Server Prerequisites

Install Docker and Compose on the server.

Create a deployment directory:

```bash
sudo mkdir -p /opt/authservice/certs
sudo chown -R $USER:$USER /opt/authservice
cd /opt/authservice
```

Copy `docker-compose.production.yml` to `/opt/authservice/docker-compose.production.yml`.
CI/CD will also upload it automatically after you configure GitHub secrets.

## 2. Create Production Environment

Create:

```bash
nano /opt/authservice/.env.production
```

Use `deploy/server.env.example` as the template.

At minimum, change:

```text
POSTGRES_PASSWORD
ConnectionStrings__AuthDb
Cors__AllowedOrigins__0
OpenIddict__Certificates__SigningCertificatePassword
OpenIddict__Certificates__EncryptionCertificatePassword
```

For the built-in Compose PostgreSQL service, the connection string host is:

```text
Host=postgres
```

## 3. Add OpenIddict Certificates

Production requires two PFX files:

```text
/opt/authservice/certs/authservice-signing.pfx
/opt/authservice/certs/authservice-encryption.pfx
```

For a test server only, you can generate local-style certificates:

```bash
./deploy/generate-compose-certs.sh
scp deploy/certs/authservice-signing.pfx user@server:/opt/authservice/certs/
scp deploy/certs/authservice-encryption.pfx user@server:/opt/authservice/certs/
```

For real production, use certificates generated and rotated through your secure secret process.

## 4. First Manual Run

On the server:

```bash
cd /opt/authservice
docker compose -f docker-compose.production.yml up -d
docker compose -f docker-compose.production.yml ps
curl -i http://127.0.0.1:8080/health/ready
```

The `migrate` service runs first and applies EF Core migrations using the migration bundle inside the Docker image.

## 5. Reverse Proxy

Run AuthService behind HTTPS. Example Nginx server block:

```nginx
server {
    listen 443 ssl http2;
    server_name auth.example.com;

    ssl_certificate /etc/letsencrypt/live/auth.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/auth.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

AuthService already reads forwarded headers.

## 6. GitHub Secrets

In GitHub:

```text
Repository -> Settings -> Secrets and variables -> Actions -> New repository secret
```

Required for deploy:

```text
DEPLOY_HOST       server IP or DNS name
DEPLOY_USER       SSH user
DEPLOY_SSH_KEY    private SSH key for that user
DEPLOY_PATH       /opt/authservice
PRODUCTION_ENV    full contents of /opt/authservice/.env.production
```

Required only if the GHCR package is private:

```text
GHCR_USERNAME     GitHub username
GHCR_TOKEN        GitHub personal access token with package read permission
```

If `DEPLOY_HOST` is not configured, CI still builds and pushes the Docker image, but deploy is skipped.

## 7. CI/CD Flow

On every pull request to `main`:

```text
restore -> build -> test
```

On every push to `main`:

```text
restore -> build -> test -> docker build -> push GHCR image -> SSH deploy
```

The deployed image is pinned to the exact Git commit SHA.

## 8. Operational Commands

Check containers:

```bash
cd /opt/authservice
docker compose -f docker-compose.production.yml ps
```

Read logs:

```bash
docker logs -f authservice-api
docker logs -f authservice-postgres
```

Restart:

```bash
docker compose -f docker-compose.production.yml up -d
```

Rollback to a previous image:

```bash
AUTHSERVICE_IMAGE=ghcr.io/ibrohimodinaev/authserviceyovar:<old-sha> \
  docker compose -f docker-compose.production.yml up -d
```

## 9. Production Notes

- Do not expose PostgreSQL to the public internet.
- Keep `.env.production` and PFX files out of git.
- Use HTTPS at the reverse proxy.
- Keep `OpenIddict__SeedOnStartup=false` in real production.
- Enable cleanup on one instance only.
- Configure backups for the PostgreSQL volume/database before real users use the system.
