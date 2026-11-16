# NEXT_PUBLIC_API_URL wird zur Build-Zeit fest ins Client-Bundle eingebacken
# (siehe lib/api.ts) - deshalb als Build-ARG statt als Laufzeit-Env-Var.
# Prod und Test brauchen dadurch JEWEILS ein eigenes Image (unterschiedliche
# Backend-URL), nicht nur unterschiedliche Container-Konfiguration.
FROM node:26-slim AS build
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
# Umgebungs-Label ("TEST" für die Test-Instanz, leer für Prod). Wird im
# Client-Bundle verwendet, um Test-Umgebung visuell zu kennzeichnen und
# den Demo-Login freizuschalten (siehe components/env-badge.tsx).
ARG NEXT_PUBLIC_ENV_LABEL=
ENV NEXT_PUBLIC_ENV_LABEL=$NEXT_PUBLIC_ENV_LABEL
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Der Build ist NICHT allein aus der Quelle bestimmt: next build holt den
# Prüfungsordnungs-Katalog über die API und backt daraus Seiten und Sitemap
# (generateStaticParams, app/sitemap.ts). Docker weiß davon nichts und würde
# bei unveränderter Quelle die alte Build-Schicht wiederverwenden - ein Deploy
# nach einer reinen Datenänderung im Backend bliebe damit wirkungslos, egal wie
# oft man ihn wiederholt (am 2026-08-23 genau so passiert). BUILD_REF wechselt
# je Deploy und erzwingt deshalb einen echten Neubau. npm ci liegt davor und
# bleibt gecacht - neu läuft nur der Next-Build.
ARG BUILD_REF=lokal
ENV BUILD_REF=$BUILD_REF

# Commit und Bauzeitpunkt für die Versionsanzeige (siehe lib/build-info.ts).
# Sie stehen bewusst HIER und nicht oben bei NEXT_PUBLIC_API_URL: Beide Werte
# wechseln bei jedem Deploy, und ein ARG entwertet den Docker-Cache ab seiner
# Zeile. Weiter oben würde damit bei jedem Deploy auch "npm ci" neu laufen -
# minutenlang, ohne dass sich eine einzige Abhängigkeit geändert hätte.
# Ohne Werte bleibt die Anzeige stumm statt zu raten.
ARG NEXT_PUBLIC_BUILD_COMMIT=
ENV NEXT_PUBLIC_BUILD_COMMIT=$NEXT_PUBLIC_BUILD_COMMIT
ARG NEXT_PUBLIC_BUILD_TIME=
ENV NEXT_PUBLIC_BUILD_TIME=$NEXT_PUBLIC_BUILD_TIME

RUN npm run build

FROM node:26-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production

# Der Standalone-Build (output: "standalone" in next.config.ts) enthält server.js
# und nur die Pakete, die der Server zur Laufzeit lädt - kein vollständiges
# node_modules, kein Compiler. Er bringt weder public/ noch .next/static mit;
# beides wird danebengelegt (public/ enthält u. a. den Service Worker sw.js und
# die Kartendateien, .next/static die Skripte und Stile des Browsers). Die
# Build-Kennung für den Service Worker (NEXT_PUBLIC_BUILD_COMMIT) steckt schon
# im Browser-Bundle und braucht zur Laufzeit nichts. Bewusst NICHT kopiert:
# .next/cache (Build-Zwischenspeicher).
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

# TLS übernimmt Caddy als Reverse Proxy (siehe Caddyfile) - der Container
# selbst spricht nur unverschlüsseltes HTTP innerhalb des Docker-Netzwerks,
# kein eigenes Zertifikat/server.mjs nötig (das ist nur für lokales
# HTTPS-Testen auf dem Entwicklungsrechner gedacht, siehe README.md).
ENV PORT=3000
# Auf allen Schnittstellen lauschen, damit Caddy den Container im Compose-Netz erreicht.
ENV HOSTNAME=0.0.0.0
EXPOSE 3000

CMD ["node", "server.js"]
