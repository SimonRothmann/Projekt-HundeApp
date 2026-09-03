import type { Metadata } from "next";
import { NeuerungenInhalt } from "./neuerungen-inhalt";

/**
 * Server-Hülle. metadata darf nur aus einer Server-Komponente kommen,
 * der Inhalt braucht dagegen den Übersetzer und damit den Client-Baum.
 * Gerendert wird er weiterhin vorab - für Suchmaschinen ändert sich
 * nichts, sie sehen dieselbe fertige Seite wie zuvor.
 */
export const metadata: Metadata = {
  title: "Neuerungen – was sich in Dogity geändert hat",
  description:
    "Alle Änderungen an Dogity mit Datum und Fassungsnummer: neue Funktionen, Verbesserungen und behobene Fehler.",
  alternates: { canonical: "/neuerungen" },
};

export default function NeuerungenPage() {
  return <NeuerungenInhalt />;
}
