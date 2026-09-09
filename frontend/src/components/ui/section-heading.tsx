import type { LucideIcon } from "lucide-react"
import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/**
 * Überschrift eines Seitenabschnitts.
 *
 * Bis hierher war jede Abschnittsüberschrift ein blankes
 * `<h2 className="text-lg font-semibold">`. Auf der Hundeseite stehen davon
 * mehrere untereinander, dazwischen Karten mit eigenen Titeln in fast
 * derselben Größe - beim Überfliegen ließ sich nicht erkennen, wo ein neuer
 * Abschnitt anfängt und wo noch der alte läuft.
 *
 * Das eingefärbte Symbol davor ist deshalb kein Schmuck, sondern der
 * Ankerpunkt: Es kommt NUR am Anfang eines Abschnitts vor und nirgends
 * innerhalb. Wer die Seite überfliegt, findet daran die Gliederung.
 */
export function SectionHeading({
  icon: Icon,
  title,
  action,
  id,
  className,
}: {
  icon: LucideIcon
  title: ReactNode
  /** Knopf am rechten Rand, z.B. "Training erfassen". */
  action?: ReactNode
  id?: string
  className?: string
}) {
  return (
    // flex-wrap, weil Überschrift und Knopf bei großer Schrift nicht mehr
    // nebeneinander passen (Profil -> Schriftgröße).
    <div id={id} className={cn("flex flex-wrap items-center justify-between gap-2", className)}>
      <h2 className="flex min-w-0 items-center gap-2 font-heading text-lg font-semibold tracking-tight">
        <span
          className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-primary/12 text-primary ring-1 ring-primary/20"
          aria-hidden
        >
          <Icon className="size-4" />
        </span>
        <span className="min-w-0">{title}</span>
      </h2>
      {action}
    </div>
  )
}
