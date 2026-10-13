#!/bin/bash
# Runs once, when the data volume is first created. Two roles (security spec, least privilege):
#   basachai_migrator owns the database and applies migrations;
#   basachai_api only reads and writes rows (the `migrate` step grants it table rights).
set -euo pipefail
: "${MIGRATOR_PASSWORD:?set in db.env}" "${API_DB_PASSWORD:?set in db.env}"

# Passwords are read from the environment inside psql (\getenv), so they never appear in a process list.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres -v db="$POSTGRES_DB" <<'SQL'
\getenv mig_pw MIGRATOR_PASSWORD
\getenv api_pw API_DB_PASSWORD
CREATE ROLE basachai_migrator LOGIN PASSWORD :'mig_pw';
CREATE ROLE basachai_api LOGIN PASSWORD :'api_pw';
ALTER DATABASE :"db" OWNER TO basachai_migrator;
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO basachai_api;
SQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'SQL'
ALTER SCHEMA public OWNER TO basachai_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL
