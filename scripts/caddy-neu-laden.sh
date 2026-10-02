#!/usr/bin/env bash
# Caddy übernimmt den Caddyfile aus dem gerade gezogenen Stand - ohne Neustart.
#
# Läuft AUF DER VPS, aufgerufen von deploy-test.sh und deploy-prod.sh nach dem
# git-Pull. Der Ordner deploy/caddy ist als Ganzes eingebunden (nicht die
# einzelne Datei), daher sieht der Container die Datei, die git neu angelegt
# hat. `caddy reload` prüft sie vorher und tauscht sie ohne Unterbrechung aus;
# ist sie fehlerhaft, läuft Caddy mit der alten weiter. Ohne Änderung tut der
# Aufruf nichts.
#
# Der erste Aufruf nach der Umstellung vom Datei- auf den Ordner-Mount legt den
# Container einmal neu an (wenige Sekunden Pause auf allen vier Domains);
# danach ist `up -d` ein No-op. --no-deps, damit compose dabei keine
# App-Container nach der Definition dieses Branches neu anlegt.
set -euo pipefail

cd "${REPO_DIR:-/opt/dogity}"

echo "==> Caddy: Einbindung aktuell halten"
docker compose up -d --no-deps caddy

# Sieht der Container eine andere Datei als der Host, hängt die Einbindung an
# einem gelöschten Ordner (etwa wenn ein Branch-Wechsel deploy/caddy kurz
# entfernt hat). Dann hilft nur Neuanlegen - vorher prüfen, denn ein Fehler
# in der Datei legte sonst alle vier Domains lahm.
if ! docker compose exec -T caddy cat /etc/caddy/Caddyfile 2>/dev/null | cmp -s - deploy/caddy/Caddyfile; then
  echo "    Einbindung veraltet - Caddyfile prüfen und Caddy neu anlegen"
  docker compose run --rm --no-deps caddy caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
  docker compose up -d --no-deps --force-recreate caddy
fi

echo "==> Caddy: Caddyfile neu laden"
for i in $(seq 1 15); do
  if docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile; then
    echo "    Caddy hat den aktuellen Caddyfile übernommen"
    exit 0
  fi
  # Direkt nach dem Neuanlegen ist die Admin-Schnittstelle evtl. noch nicht da.
  sleep 2
done

echo "    !!! Caddy hat den Caddyfile NICHT übernommen - läuft mit dem alten weiter." >&2
echo "    Fehler ansehen: docker compose logs --since 5m caddy" >&2
exit 1
