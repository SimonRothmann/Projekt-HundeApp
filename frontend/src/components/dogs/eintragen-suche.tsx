"use client";

import { useMemo, useState, type FormEvent } from "react";
import { ArrowLeft, Check, Pencil } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { eigeneUebungAngebot, inhaltsSchluessel, sucheUebungen, type KatalogGruppe } from "@/lib/eintragen";
import { useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Exercise, Sport } from "@/lib/types";
import { cn } from "@/lib/utils";

const ZEILE =
  "flex min-h-11 w-full min-w-0 items-center gap-2 rounded-lg border border-surface-border bg-surface px-3 py-2 text-left text-sm transition-colors hover:border-primary/40";

/**
 * "+ Andere Übung": EINE durchsuchbare Liste über alle Sportarten des Hundes,
 * gruppiert nach Sportart. Ersetzt die zwei Auswahlfelder des alten Formulars
 * (erst "Sportart" mit bis zu 17 Einträgen, dann "Übung") - der Weg zu einer
 * Übung ist jetzt: Tippen, Suchen oder Scrollen, Antippen.
 *
 * Fehlt die Übung im Katalog, steht die eigene Bezeichnung als Zeile bereit:
 * ganz oben, wenn nichts trifft, sonst am Ende der Liste.
 */
export function EintragenSuche({
  sports,
  katalog,
  gewaehlt,
  offline,
  onWaehle,
  onFreitext,
  onZurueck,
}: {
  sports: readonly Sport[];
  katalog: Record<string, Exercise[]>;
  /** Schlüssel (inhaltsSchluessel) der schon gewählten Übungen - sie bekommen einen Haken. */
  gewaehlt: ReadonlySet<string>;
  offline: boolean;
  onWaehle: (uebung: Exercise) => void;
  onFreitext: (text: string) => void;
  onZurueck: () => void;
}) {
  const t = useT();
  const [suche, setSuche] = useState("");

  const gruppen = useMemo<KatalogGruppe[]>(
    () => sports.filter((sport) => katalog[sport.id]).map((sport) => ({ sport, uebungen: katalog[sport.id] })),
    [sports, katalog],
  );
  const treffer = useMemo(() => sucheUebungen(gruppen, suche), [gruppen, suche]);
  const angebot = eigeneUebungAngebot(suche, treffer);
  const anzahl = treffer.reduce((summe, gruppe) => summe + gruppe.uebungen.length, 0);
  // Mit nur einer Sportart sind Gruppenköpfe Wiederholung - der Hund hat dann
  // eben diese eine, und sie steht schon im Kopf der Übungsliste.
  const mitKoepfen = sports.length > 1;
  const laedt = sports.length > 0 && gruppen.length === 0;

  function absenden(e: FormEvent) {
    e.preventDefault();
    // Enter nimmt das Eindeutige: den einen Treffer, sonst die eigene Bezeichnung.
    if (angebot?.oben) onFreitext(angebot.text);
    else if (anzahl === 1) onWaehle(treffer[0].uebungen[0]);
  }

  const eigeneZeile = angebot && (
    <button type="button" className={cn(ZEILE, "border-dashed")} onClick={() => onFreitext(angebot.text)}>
      <Pencil className="size-4 shrink-0 text-primary-text" aria-hidden />
      <span className="min-w-0 [overflow-wrap:anywhere]">{t("Eigene Übung: „{text}“ eintragen", { text: angebot.text })}</span>
    </button>
  );

  return (
    // Mindesthöhe: Das Fenster soll beim Tippen nicht mit der Trefferzahl schrumpfen
    // und springen - aber auch bei offener Tastatur (kleiner sichtbarer Bereich) noch passen.
    <div className="flex min-h-64 flex-1 flex-col">
      <form onSubmit={absenden} className="flex shrink-0 items-center gap-1 px-4 pb-3" role="search">
        <Button type="button" variant="ghost" size="icon" className="size-11 shrink-0" onClick={onZurueck} aria-label={t("Zurück")}>
          <ArrowLeft className="size-5" />
        </Button>
        <Input
          type="search"
          enterKeyHint="search"
          autoComplete="off"
          autoCapitalize="off"
          spellCheck={false}
          autoFocus
          aria-label={t("Übung suchen")}
          placeholder={t("Übung suchen oder eigene eintragen")}
          value={suche}
          maxLength={TEXTLAENGE.eigeneUebung}
          onChange={(e) => setSuche(e.target.value)}
          className="h-11"
        />
      </form>

      <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto overscroll-contain px-4 pb-4">
        {offline && gruppen.length === 0 && (
          <p className="text-sm text-muted-foreground">{t("Offline - nur freie Eingabe")}</p>
        )}
        {laedt && <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>}

        {angebot?.oben && eigeneZeile}

        {treffer.map(({ sport, uebungen }) => (
          <section key={sport.id} className="flex flex-col gap-1.5" aria-label={mitKoepfen ? sport.name : undefined}>
            {mitKoepfen && (
              <h3 className="pt-1 text-xs font-medium tracking-wide text-muted-foreground uppercase [overflow-wrap:anywhere]">
                {sport.name}
              </h3>
            )}
            {uebungen.map((uebung) => {
              const schonDa = gewaehlt.has(inhaltsSchluessel(uebung.id, uebung.name));
              return (
                <button key={uebung.id} type="button" className={ZEILE} onClick={() => onWaehle(uebung)}>
                  <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{uebung.name}</span>
                  {schonDa && <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-500" aria-label={t("schon gewählt")} />}
                </button>
              );
            })}
          </section>
        ))}

        {angebot && !angebot.oben && eigeneZeile}

        {/* Der Weg zur eigenen Übung, solange noch nichts getippt ist: ohne den
            Hinweis suchte man ihn in der Liste. */}
        {!suche.trim() && !laedt && (
          <p className="text-xs text-muted-foreground">{t("Nicht dabei? Tippe oben den Namen deiner Übung ein.")}</p>
        )}
      </div>
    </div>
  );
}
