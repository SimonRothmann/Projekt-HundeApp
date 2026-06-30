#!/usr/bin/env bash
# Deployt Prod+Test auf die VPS (siehe deploy/README.md). Synct das
# Repository per rsync und baut die Docker-Images DIREKT AUF DER VPS (kein
# eigenes Image-Registry nötig - einfacher für einen einzelnen kleinen
# Server, siehe DEPLOYMENT.md "Deployment Flow"). Migrationen + Sportarten-
# /Übungskatalog laufen automatisch beim Containerstart (siehe Program.cs),
# kein separater Migrationsschritt nötig.
#
# Voraussetzung: SSH-Zugriff auf die VPS, .env liegt dort bereits unter
# $REMOTE_DIR/.env (wird NICHT von diesem Skript übertragen, siehe
# .env.example - einmalig manuell auf die VPS kopieren und mit echten
# Werten befüllen).
set -euo pipefail

VPS_HOST="${VPS_HOST:?Setze VPS_HOST, z.B. VPS_HOST=user@123.45.67.89}"
REMOTE_DIR="${REMOTE_DIR:-/opt/dogity}"

echo "==> Lokale Checks (Backend)"
( cd backend && dotnet build && dotnet test )

echo "==> Lokale Checks (Frontend)"
( cd frontend && npx tsc --noEmit && npm run lint && npm run build )

echo "==> Sync nach $VPS_HOST:$REMOTE_DIR"
rsync -az --delete \
  --exclude='.git' \
  --exclude='.env' \
  --exclude='backend/**/bin' --exclude='backend/**/obj' \
  --exclude='frontend/node_modules' --exclude='frontend/.next' \
  --exclude='certs' \
  ./ "$VPS_HOST:$REMOTE_DIR/"

echo "==> Build + Start auf der VPS"
ssh "$VPS_HOST" "cd $REMOTE_DIR && docker compose build && docker compose up -d"

echo "==> Fertig. Status:"
ssh "$VPS_HOST" "cd $REMOTE_DIR && docker compose ps"
echo "==> Logs pruefen mit: ssh $VPS_HOST 'cd $REMOTE_DIR && docker compose logs -f backend-prod backend-test'"
