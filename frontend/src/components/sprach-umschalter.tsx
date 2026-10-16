"use client";

import { Languages } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth-context";
import { useSprache, useT } from "@/lib/i18n";
import { speichereLokaleSprache } from "@/lib/i18n/lokale-sprache";
import { SPRACHE_NAME, SPRACHEN } from "@/lib/i18n/sprachen";
import { cn } from "@/lib/utils";

/**
 * Schaltet die Oberfläche zwischen Deutsch und English um - für alle, die
 * (noch) kein Konto haben: Startseite, Anmeldung, Registrierung.
 *
 * Er setzt NUR die lokale Wahl im Browser (lib/i18n/lokale-sprache.ts). Für
 * Angemeldete blendet er sich aus: Bei ihnen steht die Kontoeinstellung in der
 * Reihenfolge vor der lokalen Wahl, ein Klick hier bliebe also wirkungslos -
 * oder überschriebe still, was im Profil gewählt wurde. Wer angemeldet ist,
 * ändert die Sprache wie bisher unter Profil → Sprache.
 *
 * Ein einziger Knopf statt zweier: Die Kopfzeile ist bei 375 px schon mit
 * Anmelden und "Kostenlos starten" voll. Der Knopf zeigt die geltende Sprache
 * (Kürzel), der Name der anderen steht im Hilfstext.
 */
export function SprachUmschalter({ className }: { className?: string }) {
  const { user, isLoading } = useAuth();
  const sprache = useSprache();
  const t = useT();

  // Wie bei den Anmelde-Verweisen: Solange unklar ist, ob jemand angemeldet
  // ist, gilt der abgemeldete Zustand - der ist es für Suchmaschinen und die
  // meisten Besucher.
  if (!isLoading && user) return null;

  const ziel = SPRACHEN[(SPRACHEN.indexOf(sprache) + 1) % SPRACHEN.length];
  const beschreibung = t("Sprache: {aktuell}. Zu {ziel} wechseln", {
    aktuell: SPRACHE_NAME[sprache],
    ziel: SPRACHE_NAME[ziel],
  });

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      aria-label={beschreibung}
      title={beschreibung}
      onClick={() => speichereLokaleSprache(ziel)}
      className={cn("min-h-11 min-w-11 px-2 font-semibold uppercase", className)}
    >
      <Languages className="hidden sm:block" aria-hidden />
      {sprache}
    </Button>
  );
}
