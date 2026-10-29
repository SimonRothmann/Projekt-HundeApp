import { anteilProzent, balkenProzent } from "@/lib/kennzahlen";

/**
 * Bausteine der Nutzungsstatistik: schlichte Balken aus CSS, ohne
 * Diagramm-Paket (wie Letzte30TageSection).
 */

/**
 * Eine Zeile "Beschriftung - Zahl (Anteil) - Balken". Der Balken ist Schmuck:
 * die Zahl steht daneben im Text.
 */
export function Balkenzeile({
  label,
  wert,
  bezug,
  zusatz,
  mitAnteil = true,
  hauptzeile = false,
}: {
  label: string;
  wert: number;
  bezug: number;
  /** Kleine Zeile unter dem Balken. */
  zusatz?: string;
  mitAnteil?: boolean;
  hauptzeile?: boolean;
}) {
  const anteil = anteilProzent(wert, bezug);
  return (
    <li className="min-w-0">
      <div className="flex items-baseline justify-between gap-3 text-sm">
        <span className={hauptzeile ? "min-w-0 font-medium [overflow-wrap:anywhere]" : "min-w-0 [overflow-wrap:anywhere]"}>{label}</span>
        <span className="shrink-0 tabular-nums">
          <span className="font-semibold">{wert}</span>
          {mitAnteil && anteil !== null && <span className="ml-1.5 text-xs text-muted-foreground">{anteil} %</span>}
        </span>
      </div>
      <div aria-hidden className="mt-1 h-2 overflow-hidden rounded-full bg-primary/15">
        <div className="h-full rounded-full bg-primary" style={{ width: `${balkenProzent(wert, bezug)}%` }} />
      </div>
      {zusatz && <p className="mt-1 text-xs text-muted-foreground">{zusatz}</p>}
    </li>
  );
}

/**
 * Stehende Säulen in einer Reihe, jede gleich breit (`flex-1`): 24 Stunden
 * passen so auch auf 375 px ohne seitliches Scrollen. Beschriftet wird nur
 * jede `beschriftenAb`-te Säule; die Zahlen selbst stehen für Screenreader im
 * `aria-label` (und als Tooltip), die Säulen sind Schmuck.
 */
export function Saeulen({
  werte,
  labels,
  beschriftenJede = 1,
  beschreibung,
}: {
  werte: readonly number[];
  labels: readonly string[];
  /** 1 = jede Säule, 3 = jede dritte ... */
  beschriftenJede?: number;
  /** Eine Zeile, die das Diagramm in Worten beschreibt (Screenreader). */
  beschreibung: string;
}) {
  const hoechster = Math.max(0, ...werte);
  return (
    <div role="img" aria-label={`${beschreibung}: ${werte.map((w, i) => `${labels[i]} ${w}`).join(", ")}`}>
      <div className="flex h-24 items-end gap-0.5" aria-hidden>
        {werte.map((wert, i) => (
          <div key={i} className="flex h-full min-w-0 flex-1 items-end" title={`${labels[i]}: ${wert}`}>
            <div
              className="w-full rounded-t-sm bg-primary"
              style={{ height: `${balkenProzent(wert, hoechster)}%` }}
            />
          </div>
        ))}
      </div>
      <div className="mt-1 flex gap-0.5 text-[10px] leading-none text-muted-foreground" aria-hidden>
        {labels.map((label, i) => (
          <span key={i} className="flex h-3 min-w-0 flex-1 justify-center whitespace-nowrap">
            {i % beschriftenJede === 0 ? label : ""}
          </span>
        ))}
      </div>
    </div>
  );
}
