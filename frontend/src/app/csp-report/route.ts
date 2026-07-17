/**
 * Empfängt CSP-Verletzungsberichte (siehe report-uri in next.config.ts) und
 * loggt sie nach stdout - auf der VPS also in die Frontend-Container-Logs
 * (`docker compose logs frontend-prod|frontend-test`). Same-origin bewusst:
 * kein CORS nötig, jede Umgebung sammelt ihre eigenen Reports, und die
 * Reports sind auch von Geräten ohne erreichbare Browser-Konsole (iPhone-
 * PWA) zentral einsehbar.
 *
 * Kein Auth: Browser senden CSP-Reports ohne Credentials. Gegen Missbrauch
 * (Log-Flutung) ist der gelesene Body hart gekappt; mehr Schutz braucht ein
 * reiner Beobachtungs-Endpoint für die Report-Only-Woche nicht.
 */
const MAX_REPORT_BYTES = 8_192;

export async function POST(request: Request) {
  let body = "";
  try {
    body = (await request.text()).slice(0, MAX_REPORT_BYTES);
  } catch {
    // Abgebrochener/kaputter Request-Body - nichts zu loggen.
  }

  if (body) {
    // Einzeilig loggen, damit `docker logs | grep CSP-Report` alles findet.
    console.warn(`[CSP-Report] ${body.replaceAll("\n", " ")}`);
  }

  return new Response(null, { status: 204 });
}
