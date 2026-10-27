#!/bin/bash
set -euo pipefail
umask 077
test -s /run/secrets/database-password
chmod 0700 "$PGDATA"
export PGUSER=tripsidekick_admin
export PGPASSWORD
PGPASSWORD="$(cat /run/secrets/database-password)"

if [[ ! -s "$PGDATA/PG_VERSION" ]]; then
    initdb --username=tripsidekick_admin --pwfile=/run/secrets/database-password \
        --auth-local=scram-sha-256 --auth-host=scram-sha-256
fi
if [[ "$(cat "$PGDATA/PG_VERSION")" != 18 ]]; then
    echo "PostgreSQL 18 data directory required; use an explicit upgrade procedure." >&2
    exit 78
fi
cat >"$PGDATA/pg_hba.conf" <<'HBA'
local all all scram-sha-256
host all tripsidekick_admin 127.0.0.1/32 scram-sha-256
host tripsidekick tripsidekick 0.0.0.0/0 scram-sha-256
HBA
pg_ctl --pgdata="$PGDATA" --wait \
    --options="-c listen_addresses='' -c unix_socket_directories=/tmp" start
trap 'pg_ctl --pgdata="$PGDATA" --mode=fast --wait stop' EXIT
psql --host=/tmp --dbname=postgres --no-password --set=ON_ERROR_STOP=1 <<'SQL'
SET log_min_error_statement=panic;
\set app_password `cat /run/secrets/database-password`
SELECT 'CREATE ROLE tripsidekick LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION'
    WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname='tripsidekick') \gexec
SELECT format('ALTER ROLE tripsidekick PASSWORD %L', :'app_password') \gexec
SELECT 'CREATE DATABASE tripsidekick OWNER tripsidekick'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='tripsidekick') \gexec
SQL
pg_ctl --pgdata="$PGDATA" --mode=fast --wait stop
trap - EXIT
unset PGPASSWORD
exec postgres -D "$PGDATA" -c listen_addresses='*' -c unix_socket_directories=/tmp
