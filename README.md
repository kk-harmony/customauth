# CustomOAuthServer

OAuth 2.0 / OpenID Connect authorization server built with **ASP.NET Core 10**, **OpenIddict 6.2.1**, **PostgreSQL**, and **Dapper** (application users).

## Architecture

```
CustomOAuthServer.Api          ? HTTP endpoints, middleware, Serilog
CustomOAuthServer.Application  ? domain models, interfaces, options
CustomOAuthServer.Infrastructure ? OpenIddict, EF Core (OpenIddict store), Dapper (users)
```

| Layer | Responsibility |
|-------|----------------|
| **Api** | `/connect/*`, `/api/me`, `/health`, `/ready`, login UI |
| **Application** | `IUserRepository`, configuration options |
| **Infrastructure** | PostgreSQL, OpenIddict, Dapper users, deployment-time SQL migrations |

Protected APIs validate **JWT access tokens** via **OpenIddict.Validation** (Bearer tokens from this authority).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 16+ (local or Docker)
- Docker (optional, for `docker-compose` and integration tests)

## Configuration

**Development** uses `appsettings.Development.json` for connection strings and seed secrets.

**Production** requires environment variables only ù see **[PRODUCTION.md](PRODUCTION.md)**.

**Fly.io** ù see **[FLY.md](FLY.md)** for Fly secrets and `*.fly.dev` deployment.


| Key | Description |
|-----|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL (dev: appsettings; prod: **env required**) |
| `OAuthServer__Issuer` | Canonical issuer URL |
| `OAuthServer__CorsRootDomain` | Production: root domain + all subdomains for CORS |
| `OAuthServer__AllowedOrigins__*` | Optional explicit CORS origins (Production) |
| `OAuthServer__SigningCertificatePath` | PFX for signing (Production env) |
| `Serilog__LogFilePath` | Rolling log file path |

### CORS

| Environment | Behavior |
|-------------|----------|
| Development | Any origin (supports credentials) |
| Production | `CorsRootDomain` + subdomains + optional `AllowedOrigins` |

### Create signing PFX for Production

Production **must** use a PKCS#12 (`.pfx`) file for OpenIddict token signing (not development auto-certs). Set:

- `OAuthServer__SigningCertificatePath` ù path to the `.pfx` inside the container or host
- `OAuthServer__SigningCertificatePassword` ù export password

**Never commit** `.pfx`, `.key`, or passwords to git (`*.pfx` is in `.gitignore`).

#### Option A ù OpenSSL (self-signed or internal CA)

Suitable for staging, Fly.io `*.fly.dev`, or internal IdP. For public-facing production, prefer a CA-issued certificate (Option B).

```bash
CERT_DIR=./certs
APP_HOST=customoauth.fly.dev          # your public OAuth hostname (Fly or custom domain)
PFX_PASSWORD='choose-a-strong-password'

mkdir -p "$CERT_DIR"

# 1. RSA private key (4096-bit)
openssl genrsa -out "$CERT_DIR/signing.key" 4096

# 2. Self-signed certificate (adjust validity and subject as needed)
openssl req -new -x509 \
  -key "$CERT_DIR/signing.key" \
  -out "$CERT_DIR/signing.crt" \
  -days 825 \
  -subj "/CN=CustomOAuthServer Signing/O=YourOrg" \
  -addext "subjectAltName=DNS:${APP_HOST},DNS:localhost"

# 3. Export PKCS#12 (.pfx) for OpenIddict
openssl pkcs12 -export \
  -out "$CERT_DIR/signing.pfx" \
  -inkey "$CERT_DIR/signing.key" \
  -in "$CERT_DIR/signing.crt" \
  -password "pass:${PFX_PASSWORD}"

# 4. Restrict file permissions
chmod 600 "$CERT_DIR/signing.key" "$CERT_DIR/signing.pfx"
```

Configure the running app (or Fly secrets ù see [FLY.md](FLY.md)):

```bash
export OAuthServer__SigningCertificatePath="$(pwd)/certs/signing.pfx"
export OAuthServer__SigningCertificatePassword="${PFX_PASSWORD}"
```

Optional separate encryption certificate (defaults to signing cert if omitted):

```bash
export OAuthServer__EncryptionCertificatePath="$OAuthServer__SigningCertificatePath"
export OAuthServer__EncryptionCertificatePassword="$PFX_PASSWORD"
```

#### Option B ù CA-issued certificate

1. Generate a CSR with your org process (or use the key from Option A step 1 and submit CSR to your CA).
2. Receive the signed certificate (`.cer` / `.pem`).
3. Build the PFX including the full chain:

```bash
openssl pkcs12 -export \
  -out certs/signing.pfx \
  -inkey certs/signing.key \
  -in certs/signing.crt \
  -certfile certs/ca-chain.pem \
  -password "pass:${PFX_PASSWORD}"
```

#### Deploying the PFX

| Target | Notes |
|--------|--------|
| **Docker / VM** | Mount `certs/signing.pfx` read-only (e.g. `/run/secrets/signing.pfx`) |
| **Fly.io** | Auto-generated on first deploy to `/data/certs` volume; see [FLY.md](FLY.md) |
| **Rotation** | New PFX invalidates existing tokens; plan overlap or maintenance window |

## Database schema

1. **App users** ù SQL scripts in `Migrations/Sql/` (applied on startup via `schema_migrations`).
2. **OpenIddict** ù EF Core store. On startup:

Migrations are applied by `docker compose` (`migrate` service) or `./scripts/run-database-migrations.sh` ù not at API startup.

### Generate OpenIddict EF migrations

```bash
./scripts/generate-openiddict-migrations.sh InitialOpenIddict
```

See **[PRODUCTION.md](PRODUCTION.md)** for the full production deployment checklist.

## Security features

- Login lockout after failed attempts (`Security:MaxFailedLoginAttempts`, `Security:LockoutMinutes`)
- Login rate limit (`POST /account/login`)
- Password policy on admin user create/update
- Reference refresh tokens (rotation on each use)
- Security headers (HSTS when HTTPS, X-Frame-Options, nosniff, etc.)
- Audit log (`audit_events`) for admin CRUD, login, token issuance
- Protected system clients cannot be deleted via admin API

## Run locally

```bash
docker compose up -d postgres
chmod +x scripts/*.sh
./scripts/run-database-migrations.sh
dotnet run --project src/CustomOAuthServer.Api
```

Or full stack (migrate job + API):

```bash
docker compose up --build
```

| Profile | URL |
|---------|-----|
| Local HTTPS | `https://localhost:5001` |
| Docker | `http://localhost:8080` |

### Seed clients and users

| Client ID | Secret | Flows / endpoints |
|-----------|--------|-------------------|
| `spa-client` | *(public)* | Authorization Code + PKCE, refresh, userinfo, revocation |
| `m2m-client` | `m2m-secret-change-in-production` | Client credentials, introspection, revocation |
| `obo-client` | `obo-secret-change-in-production` | Token exchange (OBO), client credentials, revocation |
| `introspection-client` | `introspection-secret-change-in-production` | Token introspection |
| `admin-client` | `admin-secret-change-in-production` | Client credentials with `admin` scope |

Default admin (SPA user management): `admin` ó password from `Seed__DefaultAdminPassword` or `ThisIsP@ss`. Use scopes `openid profile api admin offline_access` with `spa-client`.

Demo users: `alice` / `bob` ù password `Password123!`

> **Production:** See [PRODUCTION.md](PRODUCTION.md) ù env-only secrets, HTTPS issuer, signing certs, and migrations via `migrate` service.

## OAuth endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /.well-known/openid-configuration` | OIDC discovery |
| `GET/POST /connect/authorize` | Authorization (login required) |
| `POST /connect/token` | Token issuance |
| `GET /connect/userinfo` | User profile (Bearer token) |
| `POST /connect/logout` | End session |
| `POST /connect/introspect` | Token introspection (confidential client) |
| `POST /connect/revoke` | Token revocation |
| `GET /api/me` | Protected resource API |

## Admin API (user & client management)

Requires a Bearer token with the **`admin`** scope (use `admin-client`).

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/admin/users` | List users |
| `GET` | `/api/admin/users/{id}` | Get user |
| `POST` | `/api/admin/users` | Create user |
| `PUT` | `/api/admin/users/{id}` | Update user (email, displayName, optional password) |
| `DELETE` | `/api/admin/users/{id}` | Delete user |
| `GET` | `/api/admin/clients` | List OAuth clients |
| `GET` | `/api/admin/clients/{clientId}` | Get client |
| `POST` | `/api/admin/clients` | Create client |
| `PUT` | `/api/admin/clients/{clientId}` | Update client |
| `DELETE` | `/api/admin/clients/{clientId}` | Delete client |

### Obtain an admin token

```bash
ADMIN_TOKEN=$(curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=admin-client" \
  -d "client_secret=admin-secret-change-in-production" \
  -d "scope=admin" | jq -r .access_token)
```

### Example: create a user

```bash
curl -s -X POST "$BASE/api/admin/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"username":"carol","email":"carol@example.com","password":"SecurePass123!","displayName":"Carol"}'
```

### Example: create an OAuth client

```bash
curl -s -X POST "$BASE/api/admin/clients" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "my-service",
    "displayName": "My Backend Service",
    "clientType": "confidential",
    "clientSecret": "change-me",
    "grantTypes": ["client_credentials"],
    "scopes": ["api"]
  }'
```

Supported `grantTypes`: `authorization_code`, `refresh_token`, `client_credentials`, `token_exchange`.  
`clientType`: `public` or `confidential` (secret required for confidential).

## OAuth flows (curl)

Set `BASE` to your issuer (e.g. `https://localhost:5001` or `http://localhost:8080`).

### Discovery

```bash
curl -s "$BASE/.well-known/openid-configuration" | jq .
```

### Client credentials (M2M)

```bash
curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=m2m-client" \
  -d "client_secret=m2m-secret-change-in-production" \
  -d "scope=api"
```

### Authorization code + PKCE

1. Generate `code_verifier` and S256 `code_challenge`.
2. Sign in at `/account/login`, then open:

```
$BASE/connect/authorize?client_id=spa-client&response_type=code&scope=openid%20profile%20api%20offline_access&redirect_uri=https://localhost:3000/callback&code_challenge=CHALLENGE&code_challenge_method=S256
```

3. Exchange the code:

```bash
curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=authorization_code" \
  -d "client_id=spa-client" \
  -d "code=CODE" \
  -d "redirect_uri=https://localhost:3000/callback" \
  -d "code_verifier=VERIFIER"
```

### Refresh token

```bash
curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=refresh_token" \
  -d "client_id=spa-client" \
  -d "refresh_token=REFRESH_TOKEN"
```

### On-Behalf-Of (token exchange)

```bash
SUBJECT_TOKEN=$(curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=m2m-client" \
  -d "client_secret=m2m-secret-change-in-production" \
  -d "scope=api" | jq -r .access_token)

curl -s -X POST "$BASE/connect/token" \
  -d "grant_type=urn:ietf:params:oauth:grant-type:token-exchange" \
  -d "client_id=obo-client" \
  -d "client_secret=obo-secret-change-in-production" \
  -d "subject_token=$SUBJECT_TOKEN" \
  -d "subject_token_type=urn:ietf:params:oauth:token-type:access_token" \
  -d "scope=api"
```

### Introspection

```bash
curl -s -X POST "$BASE/connect/introspect" \
  -d "token=$ACCESS_TOKEN" \
  -d "client_id=introspection-client" \
  -d "client_secret=introspection-secret-change-in-production"
```

### Revocation

```bash
curl -s -X POST "$BASE/connect/revoke" \
  -d "token=$ACCESS_TOKEN" \
  -d "token_type_hint=access_token" \
  -d "client_id=m2m-client" \
  -d "client_secret=m2m-secret-change-in-production"
```

### Protected API

```bash
curl -s "$BASE/api/me" -H "Authorization: Bearer $ACCESS_TOKEN"
```

## Health

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Liveness |
| `GET /ready` | Readiness (PostgreSQL) |

## Tests

Integration tests need PostgreSQL. The repo includes **`docker-compose.test.yml`** (Postgres 16 only).

### Recommended (Postgres container + tests)

```bash
chmod +x scripts/*.sh
./scripts/run-tests.sh
```

This starts `docker-compose.test.yml`, applies SQL migrations, sets `CUSTOMOAUTH_TEST_CONNECTION`, and runs `dotnet test`.

### Manual

```bash
docker compose -f docker-compose.test.yml up -d --wait
export CUSTOMOAUTH_TEST_CONNECTION='Host=localhost;Port=5432;Database=customoauth;Username=postgres;Password=postgres'
./scripts/apply-sql-migrations.sh
dotnet test CustomOAuthServer.sln --configuration Release
```

Other options:

1. **Docker Desktop** ù Testcontainers starts `postgres:16-alpine` automatically
2. **`docker compose up -d postgres`** from main `docker-compose.yml` (same port/database)
3. **`CUSTOMOAUTH_TEST_CONNECTION`** ù custom connection string (use **single quotes** in bash if the password contains `$`)

If PostgreSQL is unavailable, integration tests are **skipped** (reported as skipped, not passed silently).

### Test coverage

| Area | Covered |
|------|---------|
| Health / ready | Yes |
| Discovery document | Yes |
| Admin user/client CRUD | Yes |
| Client credentials + `/api/me` | Yes |
| Authorization code + PKCE + refresh | Yes |
| Token exchange (OBO) | Yes |
| UserInfo | Yes |
| Introspection + revocation | Yes |

CI runs on GitHub Actions with a PostgreSQL service container (same settings as `docker-compose.test.yml`; see `.github/workflows/ci.yml`).

## Verify

```bash
dotnet build CustomOAuthServer.sln -c Release
dotnet test CustomOAuthServer.sln --configuration Release
```

## Security notes

- Development uses auto-generated signing certificates; **Production requires a PFX** ù see [Create signing PFX for Production](#create-signing-pfx-for-production).
- Replace all default client secrets before deployment.
- Terminate TLS at a reverse proxy or bind HTTPS directly; set `OAuthServer__Issuer` to the public URL.
- Token endpoint rate limit: 60 requests/minute per IP (`POST:/connect/token`).
