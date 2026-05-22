# SQL migrations

Apply **in order** before starting the API in Production.

| Script | Purpose |
|--------|---------|
| `001_app_users.sql` | Users table |
| `002_security_tables.sql` | Login lockout and audit log |
| `003_openiddict.sql` | OpenIddict OAuth tables |
| `004_user_roles.sql` | User roles for admin scope assignment |

## Supabase (or any hosted Postgres)

1. Open **SQL Editor** in the Supabase dashboard.
2. Run each file above in order (or paste all three into one run).
3. Confirm tables exist, including `OpenIddictApplications`.

The API does **not** apply these scripts on startup in Production.

## Local / CI

```bash
export ConnectionStrings__DefaultConnection='Host=...;Port=5432;Database=...;Username=...;Password=...'
./scripts/apply-sql-migrations.sh
```

Docker Compose runs the same scripts via the `migrate` service (`Dockerfile.migrate`).
