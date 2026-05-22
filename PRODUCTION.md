# Production deployment checklist

Use this document when deploying **CustomOAuthServer** to a production environment. Development settings and secrets live in `appsettings.Development.json` only; production **must** supply configuration via environment variables.

## Before deploy

| Step | Action |
|------|--------|
| 1 | Run SQL migrations: `Migrations/Sql/001`, `002`, `003` in order (Supabase SQL Editor, or `./scripts/apply-sql-migrations.sh`) |
| 2 | Confirm OpenIddict tables exist (see [Migrations/Sql/README.md](Migrations/Sql/README.md)) |
| 3 | Provision PostgreSQL (managed or self-hosted) with TLS and backups |
| 4 | Obtain signing (and optional encryption) PFX certificates |
| 5 | Plan CORS root domain (e.g. `example.com` allows `https://app.example.com`, `https://api.example.com`) |

## Required environment variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (secrets manager) |
| `OAuthServer__Issuer` | Public HTTPS issuer URL (e.g. `https://auth.example.com/`) |
| `OAuthServer__SigningCertificatePath` | Path to signing PFX inside container/host |
| `OAuthServer__SigningCertificatePassword` | PFX password |
| `OAuthServer__CorsRootDomain` | Root domain for CORS (host + all subdomains, e.g. `example.com`) |
| `OAuthServer__AllowedOrigins__*` | Optional extra explicit origins (in addition to subdomains) |

## Recommended environment variables

| Variable | Description |
|----------|-------------|
| `OAuthServer__EncryptionCertificatePath` | Separate encryption PFX (optional) |
| `OAuthServer__EncryptionCertificatePassword` | Encryption PFX password |
| `OAuthServer__RateLimitPerMinute` | Global rate limit (default 100) |
| `Seed__Enabled` | `false` after initial bootstrap |
| `Seed__DemoUsers` | `false` |
| `Seed__ClientSecrets__*` | Only for one-time client bootstrap, then disable seed |
| `Serilog__MinimumLevel` | `Warning` or `Information` |
| `Serilog__LogFilePath` | Persistent log path |
| `Security__MaxFailedLoginAttempts` | Login lockout threshold |
| `Security__LockoutMinutes` | Lockout duration |
| `AllowedHosts` | Restrict to your public hostname(s) |

## Docker Compose (production-like)

```bash
# Set secrets via .env or export (never commit real values)
export ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=customoauth;Username=...;Password=..."
export OAuthServer__Issuer="https://auth.example.com/"
export OAuthServer__SigningCertificatePath="/run/secrets/signing.pfx"
export OAuthServer__SigningCertificatePassword="..."
export OAuthServer__CorsRootDomain="example.com"

docker compose up --build
```

The `migrate` service runs SQL scripts and EF migrations **before** the API starts.

## Security checklist

- [ ] HTTPS terminated at load balancer / ingress; `X-Forwarded-Proto` forwarded
- [ ] Signing certificates are production CA-issued or org PKI � not development certs
- [ ] `Seed__Enabled=false` and demo users disabled
- [ ] All client secrets rotated from development defaults
- [ ] `admin-client` secret stored in secrets manager; admin API restricted by network policy
- [ ] PostgreSQL credentials rotated; least-privilege DB user
- [ ] CORS `CorsRootDomain` matches your SPA/API host strategy (no `*` in production)
- [ ] `AllowedHosts` restricted
- [ ] Audit table `audit_events` monitored; retention policy defined
- [ ] Log aggregation and alerting configured

## Database migrations (operational)

| Layer | Location | Applied by |
|-------|----------|------------|
| App users & security | `001_app_users.sql`, `002_security_tables.sql` | SQL Editor / `scripts/apply-sql-migrations.sh` / Docker `migrate` |
| OpenIddict | `003_openiddict.sql` | Same (manual SQL is the supported production path) |

The API **validates** that `app_users` and OpenIddict tables exist in Production; it does **not** apply migrations on startup.

Optional: generate EF migrations later with `./scripts/generate-openiddict-migrations.sh` if you prefer `dotnet ef database update` over `003_openiddict.sql`.

## Post-deploy verification

```bash
curl -s https://auth.example.com/health
curl -s https://auth.example.com/ready
curl -s https://auth.example.com/.well-known/openid-configuration
```

- [ ] Discovery document returns 200 with correct `issuer`
- [ ] Client credentials token works for a configured M2M client
- [ ] PKCE authorization flow works from allowed CORS origin
- [ ] Introspection returns `active: true` for a valid reference token

## Rollback

1. Deploy previous API image.
2. Restore database snapshot if schema migrations were applied (EF migrations are forward-only; plan accordingly).
3. Revert certificate changes only if new keys were introduced without overlap.

## Related docs

- [FLY.md](FLY.md) � Fly.io secrets and deploy variables
- [README.md](README.md) � development setup and OAuth endpoint reference
- [.env.example](.env.example) � variable naming reference (do not use example secrets in production)
