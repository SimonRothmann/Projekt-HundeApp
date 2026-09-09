import type { LucideIcon } from "lucide-react"
import type { ReactNode } from "react"

/**
 * Beschriftung eines Blocks innerhalb einer Karte.
 *
 * Ein Trainingstag versammelt lauter verschiedene Dinge: Kommentar, Übungen,
 * Fährte, Trainer-Feedback. Sie standen alle im selben Grau und derselben
 * Größe untereinander - was wozu gehörte, war beim Überfliegen nicht zu
 * sehen. Die kleine Versalien-Zeile trennt sie voneinander, ohne wie eine
 * weitere Überschrift mit den Abschnittsüberschriften der Seite zu
 * konkurrieren (siehe SectionHeading).
 */
export function BlockLabel({ icon: Icon, children }: { icon: LucideIcon; children: ReactNode }) {
  return (
    <h4 className="flex items-center gap-1.5 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
      <Icon className="size-3.5 shrink-0 text-primary" />
      {children}
    </h4>
  )
}
