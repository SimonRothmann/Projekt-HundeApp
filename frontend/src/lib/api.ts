// Kestrel-Ports aus backend/src/Dogity.Api/Properties/launchSettings.json.
// HTTPS ist nötig, um die App von einem iPhone aus testen zu können -
// Safari verweigert navigator.geolocation außerhalb von localhost ohne
// HTTPS komplett, sogar ohne den System-Berechtigungsdialog anzuzeigen.
const BACKEND_HTTP_PORT = "5080";
const BACKEND_HTTPS_PORT = "7297";

/**
 * Ermittelt die Backend-URL. NEXT_PUBLIC_API_URL wird zur Build-Zeit fest
 * in das Client-Bundle eingebacken - "localhost" funktioniert dort aber
 * nur auf dem Entwicklungsrechner selbst. Beim Zugriff von einem anderen
 * Gerät im selben Netzwerk (z.B. Smartphone über die LAN-IP) zeigt
 * "localhost" sonst auf das Smartphone selbst statt auf den Rechner mit
 * dem Backend. Ist NEXT_PUBLIC_API_URL nicht gesetzt oder zeigt explizit
 * auf localhost/127.0.0.1, wird daher zur Laufzeit der tatsächlich
 * aufgerufene Host der Seite verwendet (gleicher Rechner, Backend-Port).
 * Der Port richtet sich nach dem Protokoll der aufrufenden Seite, nicht
 * nach dem in NEXT_PUBLIC_API_URL konfigurierten - läuft das Frontend per
 * `npm run dev:https` über HTTPS, muss auch das Backend über seinen
 * HTTPS-Port angesprochen werden, sonst schlägt die Verbindung fehl.
 */
function resolveApiUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_URL;
  if (typeof window === "undefined") return configured ?? "http://localhost:5080";

  const isLocalhostConfigured = !configured || /^https?:\/\/(localhost|127\.0\.0\.1)(:|$)/.test(configured);
  if (!isLocalhostConfigured) return configured;

  const port = window.location.protocol === "https:" ? BACKEND_HTTPS_PORT : BACKEND_HTTP_PORT;
  return `${window.location.protocol}//${window.location.hostname}:${port}`;
}

export class ApiError extends Error {
  constructor(public status: number, public errors: string[]) {
    super(errors.join(", ") || "Ein Fehler ist aufgetreten.");
  }
}

export const TOKEN_KEY = "dogity_token";
export const USER_KEY = "dogity_user";
export const REFRESH_KEY = "dogity_refresh";

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

// Räumt eine ungültig gewordene Session auf (abgelaufenes/invalides JWT und
// toter Refresh-Token) und schickt zum Login. Ohne das bleibt die App nach
// Tokenablauf in einem kaputten Zustand stecken (alte Nutzerdaten im State,
// jeder weitere Request schlägt mit 401 fehl) - Nutzer empfinden das als
// "App ist kaputt" und löschen Browserdaten, um es zu beheben, statt dass die
// App selbst reagiert.
function handleExpiredSession() {
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USER_KEY);
  window.localStorage.removeItem(REFRESH_KEY);
  if (!window.location.pathname.startsWith("/login")) {
    window.location.href = "/login";
  }
}

// Single-Flight: Laufen mehrere Requests gleichzeitig in ein 401 (typisch
// beim Seitenaufruf, der parallel mehrere Endpoints lädt), soll NUR EIN
// Refresh-Aufruf passieren - alle warten auf dasselbe Promise. Sonst würden
// mehrere parallele Rotationen mit demselben Refresh-Token die Reuse-
// Erkennung des Backends auslösen und den Nutzer fälschlich ausloggen.
let refreshInFlight: Promise<boolean> | null = null;

// Holt mit dem gespeicherten Refresh-Token einen neuen Access-Token. Bewusst
// ein roher fetch (nicht request()), damit ein 401 hier NICHT rekursiv wieder
// einen Refresh anstößt. true = neuer Token liegt im localStorage bereit.
async function tryRefreshToken(): Promise<boolean> {
  const refreshToken = window.localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;
  try {
    const res = await fetch(`${resolveApiUrl()}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return false;
    const data = (await res.json()) as { token: string; refreshToken: string };
    window.localStorage.setItem(TOKEN_KEY, data.token);
    window.localStorage.setItem(REFRESH_KEY, data.refreshToken);
    return true;
  } catch {
    // Netzwerkfehler (offline): kein Logout - der ursprüngliche Aufruf läuft
    // ohnehin in seinen eigenen Offline-Pfad (Warteschlange).
    return false;
  }
}

async function request<T>(path: string, init?: RequestInit, isRetry = false): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${resolveApiUrl()}${path}`, { ...init, headers });

  if (!res.ok) {
    // 401 mit vorhandenem Token = abgelaufener Access-Token: EINMAL versuchen,
    // ihn per Refresh-Token zu erneuern und den Request zu wiederholen. Erst
    // wenn auch das scheitert, wird die Session aufgeräumt. (Ein 401 ohne
    // Token - z.B. falsches Passwort beim Login - ist dagegen eine normale
    // Formular-Fehlermeldung und löst keinen Refresh aus.)
    if (res.status === 401 && token && !isRetry) {
      refreshInFlight ??= tryRefreshToken().finally(() => {
        refreshInFlight = null;
      });
      const refreshed = await refreshInFlight;
      if (refreshed) return request<T>(path, init, true);
      handleExpiredSession();
    }

    let errors: string[] = [`HTTP ${res.status}`];
    try {
      const body = await res.json();
      if (body?.errors) errors = body.errors;
    } catch {
      // Antwort ohne JSON-Body (z.B. 401 ohne Inhalt) - Default-Fehler beibehalten.
    }
    throw new ApiError(res.status, errors);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: body ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
};
