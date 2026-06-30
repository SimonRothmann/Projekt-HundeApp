// Custom HTTPS-Server für den Produktions-Build (next.js docs/01-app/02-guides/custom-server.md).
//
// "next start" kennt - anders als "next dev" - kein "--experimental-https",
// es gibt also keinen eingebauten Weg, einen Produktions-Build über HTTPS
// auszuliefern. Den braucht es aber für zwei Dinge, die im Dev-Modus nicht
// echt testbar sind: (1) iOS Safari verweigert Geolocation ohne Secure
// Context, (2) der Service Worker (Offline-App-Shell, siehe public/sw.js)
// registriert sich laut pwa-register.tsx nur bei NODE_ENV=production und
// wird im Dev-Modus aktiv deinstalliert - "richtig" offline (App lädt ganz
// ohne Netz, nicht nur die API-Schreibvorgänge sind gequeued) lässt sich
// also nur gegen diesen Server testen, nicht gegen "npm run dev:https".
import { createServer } from "node:https";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import next from "next";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const port = parseInt(process.env.PORT || "3000", 10);
const app = next({ dev: false, dir: __dirname });
const handle = app.getRequestHandler();

const httpsOptions = {
  key: readFileSync(path.join(__dirname, "..", "certs", "lan-key.pem")),
  cert: readFileSync(path.join(__dirname, "..", "certs", "lan-cert.pem")),
};

app.prepare().then(() => {
  createServer(httpsOptions, (req, res) => handle(req, res)).listen(port, "0.0.0.0", () => {
    console.log(`> Produktions-Build über HTTPS auf https://0.0.0.0:${port}`);
  });
});
