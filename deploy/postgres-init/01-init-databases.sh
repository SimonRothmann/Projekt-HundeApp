#!/usr/bin/env bash
# Läuft automatisch beim allerersten Start des Postgres-Containers (leeres
# Datenvolume) - das offizielle postgres-Image führt jedes Skript in
# /docker-entrypoint-initdb.d/ einmalig aus. Legt je eine Rolle+Datenbank
# für Prod und Test an, damit beide Umgebungen sich EINE Postgres-Instanz
# teilen (spart RAM gegenüber zwei Postgres-Containern) aber vollständig
# voneinander getrennte Daten haben.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-SQL
    CREATE ROLE dogity_prod LOGIN PASSWORD '${DOGITY_PROD_DB_PASSWORD}';
    CREATE DATABASE dogity_prod OWNER dogity_prod;

    CREATE ROLE dogity_test LOGIN PASSWORD '${DOGITY_TEST_DB_PASSWORD}';
    CREATE DATABASE dogity_test OWNER dogity_test;

    -- Postgres erlaubt JEDER Rolle, sich mit jeder neuen Datenbank zu
    -- verbinden (CONNECT für PUBLIC). Lesen könnte die Test-Rolle in der
    -- Prod-Datenbank zwar nichts - die Tabellen gehören dogity_prod -, aber
    -- eine Tür, die niemand braucht, bleibt zu. Test ist die Umgebung mit
    -- öffentlich bekannten Demo-Zugängen.
    REVOKE CONNECT ON DATABASE dogity_prod FROM PUBLIC;
    REVOKE CONNECT ON DATABASE dogity_test FROM PUBLIC;
SQL
