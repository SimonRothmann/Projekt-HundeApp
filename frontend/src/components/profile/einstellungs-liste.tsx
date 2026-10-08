import Link from "next/link";
import type { ReactNode } from "react";
import { ChevronRight, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Gruppierte Liste wie in den iOS-Einstellungen: eine Karte, Zeilen durch
 * feine Linien getrennt. Die Zeilen selbst sind ListenZeile.
 */
export function ListenGruppe({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        "flex flex-col divide-y divide-border overflow-hidden rounded-xl bg-card text-sm text-card-foreground ring-1 ring-foreground/10",
        className,
      )}
    >
      {children}
    </div>
  );
}

const ZEILE =
  "flex min-h-[3.25rem] w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/50 active:bg-muted";

/**
 * Eine Zeile: Symbol, Titel, rechts Wertvorschau und Pfeil. Mindestens 52 px
 * hoch, damit sie sich mit dem Daumen sicher treffen lässt.
 *
 * Mit `href` ein Link, mit `onClick` ein Knopf (öffnet z. B. ein Sheet).
 * Titel, Untertitel und Vorschau dürfen umbrechen; die Vorschau bekommt
 * höchstens die halbe Zeile, damit ein langer Wert den Titel nicht
 * verdrängt (kein waagerechtes Scrollen, auch bei 375 px). Abgeschnitten wird
 * nichts - ein halber Wert in einer Einstellungsliste sagt weniger als gar keiner.
 */
export function ListenZeile({
  href,
  onClick,
  icon: Icon,
  titel,
  untertitel,
  vorschau,
  punkt,
  punktText,
  gefahr,
  pfeil = true,
}: {
  href?: string;
  onClick?: () => void;
  icon?: LucideIcon;
  titel: string;
  untertitel?: string;
  vorschau?: string;
  /** Ungelesen-Punkt neben der Vorschau. */
  punkt?: boolean;
  /** Text für Screenreader zum Punkt. */
  punktText?: string;
  /** Rote, zentrierte Textzeile (Abmelden). */
  gefahr?: boolean;
  pfeil?: boolean;
}) {
  const inhalt = (
    <>
      {Icon && <Icon className={cn("size-5 shrink-0", gefahr ? "text-destructive" : "text-primary-text")} aria-hidden />}
      <span className="min-w-0 flex-1">
        <span className={cn("block font-medium [overflow-wrap:anywhere]", gefahr && "text-center text-destructive")}>{titel}</span>
        {untertitel && <span className="block text-sm text-muted-foreground [overflow-wrap:anywhere]">{untertitel}</span>}
      </span>
      {punkt && (
        <span className="size-2.5 shrink-0 rounded-full bg-primary">
          {punktText && <span className="sr-only">{punktText}</span>}
        </span>
      )}
      {vorschau && (
        <span className="min-w-0 max-w-[50%] text-right text-sm text-muted-foreground [overflow-wrap:anywhere]">{vorschau}</span>
      )}
      {pfeil && <ChevronRight className="size-5 shrink-0 text-muted-foreground" aria-hidden />}
    </>
  );

  if (href) {
    return (
      <Link href={href} className={ZEILE}>
        {inhalt}
      </Link>
    );
  }
  return (
    <button type="button" onClick={onClick} className={ZEILE}>
      {inhalt}
    </button>
  );
}
