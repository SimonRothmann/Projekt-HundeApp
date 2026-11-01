import type { ReactNode } from "react";

/** Kleine Überschrift eines Abschnitts im Fenster. */
export function Abschnittsname({ children, id }: { children: ReactNode; id?: string }) {
  return (
    <p id={id} className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
      {children}
    </p>
  );
}
