"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { SupervisedDog } from "@/lib/types";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { ChevronDown, ChevronRight, ChevronUp, PawPrint } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
/**
 * Die betreuten Hunde als flache Liste, direkt auf der Trainer-Übersicht.
 *
 * Vorher führte der einzige Weg zum Trainingsplan eines betreuten Hundes über
 * Gruppe öffnen -> Mitglied aufklappen -> Hund antippen -> auf der Hundeseite
 * zum Plan scrollen. Vier Schritte für etwas, das an einem Trainingsabend
 * mehrmals gebraucht wird. Hier ist es einer, und er landet direkt beim Plan
 * (#trainingsplan, siehe Hundeseite).
 *
 * Der Rückweg führt zurück hierher statt zu den eigenen Hunden (?from=).
 *
 * Mit `vorschau` zeigt die Karte nur die ersten Hunde und darunter "Alle
 * anzeigen (n)", das die Liste an Ort und Stelle aufklappt: Bei vielen
 * betreuten Hunden schob die volle Liste sonst alles Weitere der
 * Trainer-Übersicht aus dem Bild.
 */
export function SupervisedDogsSection({ vorschau }: { vorschau?: number } = {}) {
  const t = useT();
  const [dogs, setDogs] = useState<SupervisedDog[] | null>(null);
  const [alleZeigen, setAlleZeigen] = useState(false);

  useEffect(() => {
    let active = true;
    api
      .get<SupervisedDog[]>("/api/dogs/supervised")
      .then((list) => {
        if (active) setDogs(list ?? []);
      })
      .catch((err) => {
        if (active) {
          setDogs([]);
          toast.error(err instanceof ApiError ? err.message : t("Betreute Hunde konnten nicht geladen werden."));
        }
      });
    return () => {
      active = false;
    };
    // t bewusst nicht in der Liste: Der Uebersetzer wird hier nur im
    // Fehlerfall gebraucht. Stuende er drin, liefe der ganze Abruf bei
    // jedem Sprachwechsel erneut - Daten neu laden, weil ein Toast
    // anders heissen wuerde.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Ohne betreute Hunde nichts anzeigen: eine leere Karte hier hilft nicht -
  // betreut wird über die Gruppe ("Hund betreuen"), nicht von hier aus.
  if (dogs !== null && dogs.length === 0) return null;

  const gekuerzt = vorschau !== undefined && dogs !== null && dogs.length > vorschau && !alleZeigen;
  const sichtbar = dogs && gekuerzt ? dogs.slice(0, vorschau) : dogs;
  const kuerzbar = vorschau !== undefined && dogs !== null && dogs.length > vorschau;

  return (
    <Card>
      <CardHeader className="items-center">
        <CardTitle className="flex items-center gap-2 text-base">
          <PawPrint className="size-5" />
{t("Betreute Hunde")}
        </CardTitle>
        {dogs && (
          <CardAction>
            <Badge variant="secondary">{dogs.length}</Badge>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {dogs === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <ul className="flex flex-col gap-1.5">
            {sichtbar?.map((dog) => (
              <li key={dog.id}>
                <Link
                  // scroll={false}: Next scrollt sonst selbst - und zwar nach
                  // oben, weil #trainingsplan beim Seitenwechsel noch nicht
                  // existiert (die Ziele kommen erst per Netzantwort). Den
                  // Sprung übernimmt die Hundeseite, sobald der Anker da ist.
                  scroll={false}
                  href={`/dogs/${dog.id}?from=${encodeURIComponent("/trainer")}#trainingsplan`}
                  className="flex min-w-0 items-center gap-3 rounded-md px-2 py-2 transition-colors hover:bg-accent/30 coarse:min-h-11"
                >
                  <DogAvatar
                    dogId={dog.id}
                    hasImage={dog.hasImage}
                    name={dog.name}
                    className="size-9"
                    iconClassName="size-4"
                  />
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm font-medium">{dog.name}</span>
                    <span className="truncate text-xs text-muted-foreground">
                      {[dog.handlerName, dog.breed].filter(Boolean).join(" · ")}
                    </span>
                  </span>
                  {/* Ohne aktives Ziel gibt es noch keinen Plan - das ist die
                      eine Information, die vor dem Antippen zählt. */}
                  {dog.activeGoalCount === 0 && (
                    <Badge variant="outline" className="shrink-0 text-xs">
{t("kein Ziel")}
                    </Badge>
                  )}
                  <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                </Link>
              </li>
            ))}
          </ul>
        )}
        {kuerzbar && (
          <Button
            variant="ghost"
            size="sm"
            className="mt-1 w-full"
            onClick={() => setAlleZeigen((v) => !v)}
            aria-expanded={alleZeigen}
          >
            {alleZeigen ? <ChevronUp className="size-4" /> : <ChevronDown className="size-4" />}
            {alleZeigen ? t("Weniger anzeigen") : t("Alle anzeigen ({n})", { n: dogs?.length ?? 0 })}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
