#!/usr/bin/env bash
# Stoppt die lokale PostgreSQL@17-Instanz (Homebrew) für die CanisTrack-Entwicklung.
set -euo pipefail

PG_PREFIX="$(brew --prefix postgresql@17)"
DATA_DIR="$(brew --prefix)/var/postgresql@17"

"$PG_PREFIX/bin/pg_ctl" -D "$DATA_DIR" stop
