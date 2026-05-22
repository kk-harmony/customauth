#!/usr/bin/env bash
# Applies Migrations/Sql/*.sql with schema_migrations tracking.
# Requires: psql and ConnectionStrings__DefaultConnection or DATABASE_URL.
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ -n "${DATABASE_URL:-}" ]]; then
  CONN="$DATABASE_URL"
elif [[ -n "${ConnectionStrings__DefaultConnection:-}" ]]; then
  CONN="$ConnectionStrings__DefaultConnection"
else
  echo "Set DATABASE_URL or ConnectionStrings__DefaultConnection" >&2
  exit 1
fi

# Parse Npgsql-style connection string into PG* env vars for psql.
host=$(echo "$CONN" | sed -n 's/.*Host=\([^;]*\).*/\1/p')
port=$(echo "$CONN" | sed -n 's/.*Port=\([^;]*\).*/\1/p')
db=$(echo "$CONN" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
user=$(echo "$CONN" | sed -n 's/.*Username=\([^;]*\).*/\1/p')
pass=$(echo "$CONN" | sed -n 's/.*Password=\([^;]*\).*/\1/p')

export PGHOST="${host:-localhost}"
export PGPORT="${port:-5432}"
export PGDATABASE="${db:-customoauth}"
export PGUSER="${user:-postgres}"
export PGPASSWORD="${pass:-}"

SCRIPTS_DIR="$(pwd)/Migrations/Sql"
if [[ ! -d "$SCRIPTS_DIR" ]]; then
  echo "Migrations/Sql not found at $SCRIPTS_DIR" >&2
  exit 1
fi

psql -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS schema_migrations (
  name TEXT PRIMARY KEY,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);"

for f in $(ls "$SCRIPTS_DIR"/*.sql 2>/dev/null | sort); do
  name=$(basename "$f")
  applied=$(psql -tAc "SELECT 1 FROM schema_migrations WHERE name = '$name' LIMIT 1" || true)
  if [[ "$applied" == "1" ]]; then
    echo "Skip $name (already applied)"
    continue
  fi
  echo "Applying $name"
  psql -v ON_ERROR_STOP=1 -f "$f"
  psql -v ON_ERROR_STOP=1 -c "INSERT INTO schema_migrations (name) VALUES ('$name');"
done

echo "SQL migrations complete."
