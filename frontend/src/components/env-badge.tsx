/**
 * Umgebungs-Badge (z.B. "TEST"). Nur sichtbar, wenn NEXT_PUBLIC_ENV_LABEL
 * beim Build gesetzt war - in Prod leer, in Test = "TEST". Das rote Badge
 * ist bewusst laut, damit Nutzer sofort erkennen, dass sie in einer
 * Umgebung mit Wegwerf-Daten sind (kein versehentlicher Prod-Eintrag).
 *
 * NEXT_PUBLIC_* wird zur Build-Zeit ins Client-Bundle eingebacken - deshalb
 * hier lesbar ohne runtime-Behandlung.
 */
export function EnvBadge({ className = "" }: { className?: string }) {
  const label = process.env.NEXT_PUBLIC_ENV_LABEL;
  if (!label) return null;
  return (
    <span
      className={`inline-flex items-center rounded-md bg-red-600 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white shadow-sm ${className}`}
      aria-label={`Umgebung: ${label}`}
    >
      {label}
    </span>
  );
}

export const isTestEnv = process.env.NEXT_PUBLIC_ENV_LABEL === "TEST";
