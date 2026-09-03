import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import {
  AENDERUNGSART_LABEL,
  formatiereVeroeffentlichung,
  VERSIONSHINWEISE,
} from "@/lib/versionshinweise";
import { cn } from "@/lib/utils";

/**
 * Die jüngste Fassung in Kurzform: Nummer, Datum, Überschrift, die ersten
 * Punkte - und der Weg zur vollständigen Liste.
 *
 * Absichtlich nur ein Auszug. Vollständigkeit hat die Seite /neuerungen; an
 * den Stellen, an denen dieser Baustein steht (Startseite, Profil), ist die
 * Frage eine andere und kleinere: Tut sich hier überhaupt noch etwas, und
 * seit wann?
 */
export function LetzteNeuerung({
  className,
  maxPunkte = 3,
}: {
  className?: string;
  maxPunkte?: number;
}) {
  const neueste = VERSIONSHINWEISE[0];
  const auszug = neueste.aenderungen.slice(0, maxPunkte);
  const rest = neueste.aenderungen.length - auszug.length;

  return (
    <div className={cn("min-w-0", className)}>
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className="font-semibold">Version {neueste.version}</span>
        <time dateTime={neueste.datum} className="text-sm text-muted-foreground">
          {formatiereVeroeffentlichung(neueste.datum)}
        </time>
      </div>
      <p className="mt-1 text-sm font-medium text-balance">{neueste.titel}</p>

      <ul className="mt-3 flex flex-col gap-2">
        {auszug.map((aenderung, index) => (
          <li key={index} className="flex min-w-0 items-start gap-2.5">
            <Badge variant="outline" className="mt-0.5 shrink-0">
              {AENDERUNGSART_LABEL[aenderung.art]}
            </Badge>
            <span className="min-w-0 text-sm text-muted-foreground [overflow-wrap:anywhere]">{aenderung.text}</span>
          </li>
        ))}
        {/* Der Rest gehört in die Liste, die er abschneidet - nicht in den
            Link darunter. Ein Link, dessen Beschriftung auf schmalen Geräten
            umbricht, reißt sein Pfeilsymbol ans andere Zeilenende. */}
        {rest > 0 && (
          <li className="text-sm text-muted-foreground">
            … und {rest} weitere {rest === 1 ? "Änderung" : "Änderungen"} in dieser Fassung.
          </li>
        )}
      </ul>

      <Link
        href="/neuerungen"
        className="mt-4 inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
      >
        Alle Neuerungen
        <ArrowRight className="size-4" aria-hidden />
      </Link>
    </div>
  );
}
