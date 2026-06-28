/**
 * Ermittelt die Backend-URL. NEXT_PUBLIC_API_URL wird zur Build-Zeit fest
 * in das Client-Bundle eingebacken - "localhost" funktioniert dort aber
 * nur auf dem Entwicklungsrechner selbst. Beim Zugriff von einem anderen
 * Gerät im selben Netzwerk (z.B. Smartphone über die LAN-IP) zeigt
 * "localhost" sonst auf das Smartphone selbst statt auf den Rechner mit
 * dem Backend. Ist NEXT_PUBLIC_API_URL nicht gesetzt oder zeigt explizit
 * auf localhost/127.0.0.1, wird daher zur Laufzeit der tatsächlich
 * aufgerufene Host der Seite verwendet (gleicher Rechner, Backend-Port).
 */
function resolveApiUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_URL;
  if (typeof window === "undefined") return configured ?? "http://localhost:5080";

  const isLocalhostConfigured = !configured || /^https?:\/\/(localhost|127\.0\.0\.1)(:|$)/.test(configured);
  if (!isLocalhostConfigured) return configured;

  const port = configured ? new URL(configured).port || "5080" : "5080";
  return `${window.location.protocol}//${window.location.hostname}:${port}`;
}

export class ApiError extends Error {
  constructor(public status: number, public errors: string[]) {
    super(errors.join(", ") || "Ein Fehler ist aufgetreten.");
  }
}

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("canistrack_token");
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init?.headers as Record<string, string> | undefined),
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${resolveApiUrl()}${path}`, { ...init, headers });

  if (!res.ok) {
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
