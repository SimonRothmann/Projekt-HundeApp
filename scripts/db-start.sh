#!/usr/bin/env bash
# Startet die lokale PostgreSQL@17-Instanz (Homebrew) für die Dogity-Entwicklung.
#
# Auf manchen macOS-Versionen bricht "postgres" beim Start mit
# "postmaster became multithreaded during startup" ab, wenn LC_ALL/LANG
# nicht gesetzt sind. Daher hier explizit gesetzt statt dauerhaft in die
# Shell-Konfiguration einzutragen.
set -euo pipefail

PG_PREFIX="$(brew --prefix postgresql@17)"
DATA_DIR="$(brew --prefix)/var/postgresql@17"
LOG_FILE="$(brew --prefix)/var/log/postgresql@17.log"

export LC_ALL="${LC_ALL:-en_US.UTF-8}"

"$PG_PREFIX/bin/pg_ctl" -D "$DATA_DIR" -l "$LOG_FILE" start
