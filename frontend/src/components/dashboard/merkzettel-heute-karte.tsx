"use client";

import { useEffect, useState } from "react";
import { NotebookPen, StickyNote } from "lucide-react";
import { api } from "@/lib/api";
import type { Dog, MerkzettelEintrag } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { heuteIso } from "@/lib/kalendertag";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { heutigeEintraege, uhrzeitKurz } from "@/lib/merkzettel";

/** So viele Einträge zeigt die Karte je Hund; der Rest steht auf der Hundeseite. */
const MAX_ZEILEN = 3;

/**
 * Am Trainingstag, was für das heutige Training gemerkt ist: "Training heute 18:00" mit den Einträgen und den
 * Knöpfen "Training erfassen" und "Merken". An allen anderen Tagen bleibt die Startseite, wie sie war - die Karte
 * erscheint nur, wenn für einen Hund heute etwas ansteht.
 *
 * Eigener Abruf je Hund statt Teil der Startseiten-Daten: Der Merkzettel gehört nur der Familie, und fällt der
 * Abruf aus, fehlt nur diese Karte. `stand` ändert sich, wenn die Startseite neu geladen hat (etwa nach dem
 * Eintragen eines Trainings) - dann wird auch der Merkzettel neu geholt.
 */
export function MerkzettelHeuteKarte({
  hunde,
  stand,
  onEintragen,
  onMerken,
}: {
  hunde: Dog[];
  stand: unknown;
  onEintragen: (dogId: string) => void;
  onMerken: (dogId: string) => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const [eintraege, setEintraege] = useState<Record<string, MerkzettelEintrag[]>>({});
  const hundeIds = hunde.filter((h) => !h.archivedAt).map((h) => h.id).join(",");

  useEffect(() => {
    let abgebrochen = false;
    for (const dogId of hundeIds.split(",").filter(Boolean)) {
      api.get<MerkzettelEintrag[]>(`/api/dogs/${dogId}/merkzettel`).then(
        (liste) => {
          if (!abgebrochen) setEintraege((vorher) => ({ ...vorher, [dogId]: liste }));
        },
        () => {
          // Kein Zugriff oder kein Netz: ohne Karte weiter.
        },
      );
    }
    return () => {
      abgebrochen = true;
    };
  }, [hundeIds, stand]);

  const heute = heuteIso();
  const karten = hunde
    .filter((h) => !h.archivedAt)
    .map((hund) => ({ hund, heutige: heutigeEintraege(eintraege[hund.id], heute) }))
    .filter((karte) => karte.heutige.eintraege.length > 0);
  if (karten.length === 0) return null;

  return (
    <>
      {karten.map(({ hund, heutige }) => {
        const zeit = uhrzeitKurz(heutige.uhrzeit);
        const titel = zeit ? t("Training heute {zeit}", { zeit }) : t("Training heute");
        const sichtbar = heutige.eintraege.slice(0, MAX_ZEILEN);
        const mehr = heutige.eintraege.length - sichtbar.length;
        return (
          <Card key={hund.id} size="sm">
            <CardContent className="flex min-w-0 flex-col gap-3">
              <div className="min-w-0">
                <h2 className="text-base font-semibold [overflow-wrap:anywhere]">{titel}</h2>
                {hunde.filter((h) => !h.archivedAt).length > 1 && (
                  <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{hund.name}</p>
                )}
              </div>
              <ul className="flex min-w-0 flex-col gap-1.5">
                {sichtbar.map((eintrag) => (
                  <li key={eintrag.id} className="flex min-w-0 items-start gap-2 text-sm">
                    <StickyNote className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
                    <span className="min-w-0 [overflow-wrap:anywhere]">
                      {eintrag.text}
                      {eintrag.exerciseName && (
                        <span className="text-muted-foreground">
                          {" · "}
                          {katalogName({ name: eintrag.exerciseName, nameEn: eintrag.exerciseNameEn }, sprache)}
                        </span>
                      )}
                    </span>
                  </li>
                ))}
                {mehr > 0 && <li className="text-sm text-muted-foreground">{t("+{n} weitere", { n: mehr })}</li>}
              </ul>
              <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-2">
                <Button className="h-11 min-w-0 text-sm" onClick={() => onEintragen(hund.id)}>
                  <NotebookPen className="size-4" />
                  <span className="truncate">{t("Training erfassen")}</span>
                </Button>
                <Button variant="outline" className="h-11 text-sm" onClick={() => onMerken(hund.id)}>
                  <StickyNote className="size-4" />
                  <span className="truncate">{t("Merken")}</span>
                </Button>
              </div>
            </CardContent>
          </Card>
        );
      })}
    </>
  );
}
