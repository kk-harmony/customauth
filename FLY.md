# Fly.io deployment ù environment variables

Deploy **CustomOAuthServer** on [Fly.io](https://fly.io) using the default `Dockerfile`. Production configuration is **environment variables only** (see [PRODUCTION.md](PRODUCTION.md)).

Replace placeholders:

| Placeholder | Meaning |
|-------------|---------|
| `APP_NAME` | Fly app name (e.g. `customoauth`) |
| `SPA_ORIGIN` | Your frontend origin (e.g. `https://my-spa.fly.dev`) |

Public OAuth URL: **`https://APP_NAME.fly.dev/`**

---

## Quick start (secrets)

```bash
# 1. Create app, signing cert volume, and Postgres
fly apps create APP_NAME
fly volumes create certs --size 1 -a APP_NAME -r iad
fly postgres create --name APP_NAME-db
fly postgres attach APP_NAME-db -a APP_NAME

# 2. Set required secrets (edit values first)
fly secrets set -a APP_NAME \
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__DefaultConnection='Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require' \
  OAuthServer__Issuer='https://APP_NAME.fly.dev/' \
  OAuthServer__CorsRootDomain='APP_NAME.fly.dev' \
  OAuthServer__AllowedOrigins__0='SPA_ORIGIN' \
  OAuthServer__SigningCertificatePassword='YOUR_PFX_PASSWORD' \
  AllowedHosts='APP_NAME.fly.dev' \
  Seed__Enabled=false \
  Seed__DemoUsers=false

# 3. Run migrations (from your machine or CI ù see below)
# 4. Deploy (auto-creates signing.pfx on the volume on first boot)
fly deploy -a APP_NAME
```

---

## Required variables

These must be set as **Fly secrets** (or `[env]` in `fly.toml` for non-secret values). The app **fails startup** in Production if any required secret is missing.

| Variable | Example (Fly default domain) | Description |
|----------|------------------------------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Must be `Production` on Fly |
| `ConnectionStrings__DefaultConnection` | Npgsql connection string | From Fly Postgres attach or dashboard |
| `OAuthServer__Issuer` | `https://customoauth.fly.dev/` | Public HTTPS URL + **trailing slash** |
| `OAuthServer__CorsRootDomain` | `customoauth.fly.dev` | OAuth app host + `*.customoauth.fly.dev` ù **not** `fly.dev` |
| `OAuthServer__SigningCertificatePassword` | *(secret)* | PFX password; used when auto-generating the cert |
| `OAuthServer__SigningCertificatePath` | `/data/certs/signing.pfx` | Optional ù default in `fly.toml`; set by entrypoint if auto-generate is on |

### ASP.NET / hosting

| Variable | Recommended on Fly | Description |
|----------|-------------------|-------------|
| `ASPNETCORE_URLS` | `http://+:8080` | Match `internal_port` in `fly.toml` (set in Dockerfile) |
| `AllowedHosts` | `APP_NAME.fly.dev` | Restrict host header |

---

## CORS on Fly

| Scenario | Configuration |
|----------|----------------|
| SPA on **same** Fly hostname | Unusual; use explicit origin |
| SPA on **another** Fly app (`my-spa.fly.dev`) | `OAuthServer__AllowedOrigins__0=https://my-spa.fly.dev` |
| SPA on custom domain (`app.example.com`) | `OAuthServer__AllowedOrigins__0=https://app.example.com` and/or `OAuthServer__CorsRootDomain=example.com` |

**Do not** set `OAuthServer__CorsRootDomain=fly.dev` ù that would allow CORS from arbitrary `*.fly.dev` apps.

Development uses allow-all CORS; Production uses `CorsRootDomain` + `AllowedOrigins` only.

---

## PostgreSQL (`ConnectionStrings__DefaultConnection`)

Fly Postgres attach sets `DATABASE_URL` (URI format). This app expects an **Npgsql** connection string:

```
Host=<hostname>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require
```

Get values from:

```bash
fly postgres db list -a APP_NAME-db
fly secrets list -a APP_NAME   # after attach
```

Or copy the connection string from the Fly Postgres dashboard.

**SSL:** Use `SSL Mode=Require` (or `Verify Full` if you configure certs) for Fly managed Postgres.

---

## Signing certificate (PFX) ó auto-generated on deploy

The Docker **entrypoint** runs `scripts/ensure-signing-cert.sh` on each start. If `/data/certs/signing.pfx` is missing, it generates a self-signed PFX using OpenSSL (requires `OAuthServer__SigningCertificatePassword` and the **`certs` volume** in `fly.toml`).

| Variable | Default | Description |
|----------|---------|-------------|
| `OAUTH_AUTO_GENERATE_SIGNING_CERT` | `true` | Set `false` to supply your own PFX |
| `OAuthServer__SigningCertificatePath` | `/data/certs/signing.pfx` | Path on the mounted volume |

Disable auto-generation and upload your own PFX to the volume via `fly ssh console` if you use a CA-issued cert (see [README](README.md#create-signing-pfx-for-production)).

Optional encryption cert:

| Variable | Description |
|----------|-------------|
| `OAuthServer__EncryptionCertificatePath` | Defaults to signing path if omitted |
| `OAuthServer__EncryptionCertificatePassword` | Encryption PFX password |

---

## OAuth clients & seed (first deploy)

| Variable | First deploy | Steady state |
|----------|--------------|--------------|
| `Seed__Enabled` | `true` (one time) | `false` |
| `Seed__DemoUsers` | `false` | `false` |
| `Seed__DemoUserPassword` | omit in prod | omit |
| `Seed__ClientSecrets__m2m-client` | strong secret | omit when seed off |
| `Seed__ClientSecrets__obo-client` | strong secret | omit when seed off |
| `Seed__ClientSecrets__introspection-client` | strong secret | omit when seed off |
| `Seed__ClientSecrets__admin-client` | strong secret | omit when seed off |

After clients exist, set `Seed__Enabled=false` and redeploy.

**Redirect URIs:** Default seed uses `https://localhost:3000/callback`. For Fly, update `spa-client` redirect URIs (admin API or re-seed) to e.g. `https://my-spa.fly.dev/callback`.

---

## Recommended variables

| Variable | Example | Description |
|----------|---------|-------------|
| `OAuthServer__RateLimitPerMinute` | `100` | Global rate limit |
| `OAuthServer__AllowedOrigins__1` | `https://other-app.fly.dev` | Additional CORS origins |
| `Serilog__MinimumLevel` | `Warning` | Log level |
| `Security__MaxFailedLoginAttempts` | `5` | Login lockout |
| `Security__LockoutMinutes` | `15` | Lockout duration |
| `Security__MinPasswordLength` | `12` | Admin user passwords |

### Logging on Fly

Prefer **stdout** (Fly aggregates `fly logs`). Avoid file-only logging on ephemeral disks.

| Variable | Fly recommendation |
|----------|-------------------|
| `Serilog__LogFilePath` | Omit or use console-only configuration |

---

## Database migrations (not on app startup)

Run **before** or **during** deploy from CI or your laptop ù the runtime Docker image does not include `psql` / `dotnet ef`.

```bash
# Example: proxy Fly Postgres locally
fly proxy 5432 -a APP_NAME-db

export ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=5432;...'
./scripts/run-database-migrations.sh
```

Requires committed EF migrations under `src/CustomOAuthServer.Infrastructure/Migrations/`. Generate locally:

```bash
./scripts/generate-openiddict-migrations.sh InitialOpenIddict
```

---

## `fly.toml`

The repo includes **[fly.toml](fly.toml)** with `[mounts]` for `/data/certs` and auto-PFX env defaults. Set `app = "APP_NAME"` before deploy.

Secrets override `[env]` where the same key is set via `fly secrets set`.

---

## Custom domain (optional)

```bash
fly certs add auth.example.com -a APP_NAME
```

Update secrets:

```bash
fly secrets set -a APP_NAME \
  OAuthServer__Issuer='https://auth.example.com/' \
  OAuthServer__CorsRootDomain='example.com' \
  AllowedHosts='auth.example.com'
```

Update OAuth client redirect URIs to match the SPA URL.

---

## Verify after deploy

```bash
curl -s https://APP_NAME.fly.dev/health
curl -s https://APP_NAME.fly.dev/ready
curl -s https://APP_NAME.fly.dev/.well-known/openid-configuration | jq .issuer
```

Issuer in discovery must equal `OAuthServer__Issuer`.

---

## Copy-paste template (edit before running)

```bash
APP_NAME=customoauth
SPA_ORIGIN=https://my-spa.fly.dev
DB_CONN='Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require'
ISSUER="https://${APP_NAME}.fly.dev/"
PFX_PASSWORD='change-me'

fly secrets set -a "$APP_NAME" \
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__DefaultConnection="$DB_CONN" \
  OAuthServer__Issuer="$ISSUER" \
  OAuthServer__CorsRootDomain="${APP_NAME}.fly.dev" \
  OAuthServer__AllowedOrigins__0="$SPA_ORIGIN" \
  OAuthServer__SigningCertificatePassword="$PFX_PASSWORD" \
  AllowedHosts="${APP_NAME}.fly.dev" \
  Seed__Enabled=false \
  Seed__DemoUsers=false \
  Serilog__MinimumLevel=Warning
```

---

## Related docs

- [PRODUCTION.md](PRODUCTION.md) ù full production checklist
- [README.md](README.md) ù local development and OAuth endpoints
- [.env.example](.env.example) ù variable naming reference (development)
