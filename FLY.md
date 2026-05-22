# Fly.io deployment � environment variables

Deploy **CustomOAuthServer** on [Fly.io](https://fly.io) using the default `Dockerfile`. Production configuration is **environment variables only** (see [PRODUCTION.md](PRODUCTION.md)).

Replace placeholders:

| Placeholder | Meaning |
|-------------|---------|
| `APP_NAME` | Fly app name (e.g. `customoauth`) |
| `SPA_ORIGIN` | Your frontend origin (e.g. `https://my-spa.fly.dev`) |

Public OAuth URL: **`https://APP_NAME.fly.dev/`**

---

## Quick start

```bash
# 1. Create app, signing cert volume, and Postgres
fly apps create APP_NAME
fly volumes create certs --size 1 -a APP_NAME -r iad
fly postgres create --name APP_NAME-db
fly postgres attach APP_NAME-db -a APP_NAME

# 2. Edit fly.toml: app name, Issuer, CorsRootDomain, AllowedHosts, AllowedOrigins (if needed)
# 3. Set secrets only (credentials)
fly secrets set -a APP_NAME \
  ConnectionStrings__DefaultConnection='Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require' \
  OAuthServer__SigningCertificatePassword='YOUR_PFX_PASSWORD'

# 4. Run SQL in Supabase (Migrations/Sql/001, 002, 003) — see Migrations/Sql/README.md � see below)
# 5. Deploy (auto-creates signing.pfx on the volume on first boot)
fly deploy -a APP_NAME
```

---

## Required variables

Production startup fails if required values are missing. Use **`[env]` in `fly.toml`** for non-secrets and **`fly secrets set`** only for credentials.

| Variable | Where | Example (Fly default domain) |
|----------|-------|------------------------------|
| `ConnectionStrings__DefaultConnection` | **secret** | Npgsql connection string from Fly Postgres |
| `OAuthServer__SigningCertificatePassword` | **secret** | PFX password for auto-generated cert |
| `OAuthServer__Issuer` | `fly.toml` | `https://APP_NAME.fly.dev/` (trailing slash) |
| `OAuthServer__CorsRootDomain` | `fly.toml` | `APP_NAME.fly.dev` (not bare `fly.dev`) |
| `ASPNETCORE_ENVIRONMENT` | `fly.toml` | `Production` |
| `OAuthServer__SigningCertificatePath` | `fly.toml` | `/data/certs/signing.pfx` |

### ASP.NET / hosting (`fly.toml`)

| Variable | Example | Description |
|----------|---------|-------------|
| `ASPNETCORE_URLS` | `http://+:8080` | Match `internal_port` in `fly.toml` |
| `AllowedHosts` | `APP_NAME.fly.dev` | Restrict host header |
| `OAuthServer__AllowedOrigins__*` | `https://my-spa.fly.dev` | SPA origin when not under `CorsRootDomain` |
| `Seed__Enabled` | `false` | `true` only for one-time bootstrap |

---

## CORS on Fly

| Scenario | Configuration |
|----------|----------------|
| SPA on **same** Fly hostname | Unusual; use explicit origin |
| SPA on **another** Fly app (`my-spa.fly.dev`) | `OAuthServer__AllowedOrigins__0` in `fly.toml` |
| SPA on custom domain (`app.example.com`) | `OAuthServer__AllowedOrigins__0` and/or `OAuthServer__CorsRootDomain` in `fly.toml` |

**Do not** set `OAuthServer__CorsRootDomain=fly.dev` � that would allow CORS from arbitrary `*.fly.dev` apps.

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

## Signing certificate (PFX) � auto-generated on deploy

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

After clients exist, set `Seed__Enabled=false` in `fly.toml` and redeploy.

**Redirect URIs:** Default seed uses `https://localhost:3000/callback`. For Fly, update `spa-client` redirect URIs (admin API or re-seed) to e.g. `https://my-spa.fly.dev/callback`.

---

## Recommended variables

| Variable | Where | Example |
|----------|-------|---------|
| `OAuthServer__RateLimitPerMinute` | `fly.toml` | `100` |
| `OAuthServer__AllowedOrigins__1` | `fly.toml` | `https://other-app.fly.dev` |
| `Serilog__MinimumLevel` | `fly.toml` | `Warning` (default in repo `fly.toml`) |
| `Security__MaxFailedLoginAttempts` | `5` | Login lockout |
| `Security__LockoutMinutes` | `15` | Lockout duration |
| `Security__MinPasswordLength` | `12` | Admin user passwords |

### Logging on Fly

Prefer **stdout** (Fly aggregates `fly logs`). Avoid file-only logging on ephemeral disks.

| Variable | Fly recommendation |
|----------|-------------------|
| `Serilog__LogFilePath` | Omit or use console-only configuration |

---

## Database migrations (manual SQL — not on app startup)

Run SQL **before** deploy. The Fly image does not migrate the database automatically.

**Supabase:** SQL Editor — run `Migrations/Sql/001_app_users.sql`, `002_security_tables.sql`, `003_openiddict.sql` in order. See [Migrations/Sql/README.md](Migrations/Sql/README.md).

**From your machine** (with `psql`):

```bash
export ConnectionStrings__DefaultConnection='Host=...;Port=5432;...'
./scripts/apply-sql-migrations.sh
```

---

## `fly.toml`

The repo **[fly.toml](fly.toml)** sets non-secret production config in `[env]` (issuer, CORS, `AllowedHosts`, seed flags, logging). Edit `app`, URLs, and `OAuthServer__AllowedOrigins__*` before deploy.

**Secrets** (`fly secrets set`) should be limited to:

- `ConnectionStrings__DefaultConnection`
- `OAuthServer__SigningCertificatePassword`
- Optional one-time `Seed__ClientSecrets__*` during bootstrap

Secrets override `[env]` for the same key — remove duplicate secrets after moving values into `fly.toml`:

```bash
fly secrets unset -a APP_NAME \
  ASPNETCORE_ENVIRONMENT \
  OAuthServer__Issuer \
  OAuthServer__CorsRootDomain \
  AllowedHosts \
  Seed__Enabled \
  OAuthServer__AllowedOrigins__0
```

---

## Custom domain (optional)

```bash
fly certs add auth.example.com -a APP_NAME
```

Update `fly.toml` `[env]` (`OAuthServer__Issuer`, `OAuthServer__CorsRootDomain`, `AllowedHosts`) and redeploy.

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

Set `app` and `[env]` in **fly.toml**, then:

```bash
APP_NAME=customoauth
DB_CONN='Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require'
PFX_PASSWORD='change-me'

fly secrets set -a "$APP_NAME" \
  ConnectionStrings__DefaultConnection="$DB_CONN" \
  OAuthServer__SigningCertificatePassword="$PFX_PASSWORD"

fly deploy -a "$APP_NAME"
```

---

## Related docs

- [PRODUCTION.md](PRODUCTION.md) � full production checklist
- [README.md](README.md) � local development and OAuth endpoints
- [.env.example](.env.example) � variable naming reference (development)
