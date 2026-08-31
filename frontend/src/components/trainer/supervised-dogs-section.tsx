"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { SupervisedDog } from "@/lib/types";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { ChevronRight, PawPrint } from "lucide-react";
import { toast } from "sonner";

/**
 * Die betreuten Hunde als flache Liste, direkt auf der Trainerseite.
 *
 * Vorher führte der einzige Weg zum Trainingsplan eines betreuten Hundes über
 * Gruppe öffnen -> Mitglied aufklappen -> Hund antippen -> auf der Hundeseite
 * zum Plan scrollen. Vier Schritte für etwas, das an einem Trainingsabend
 * mehrmals gebraucht wird. Hier ist es einer, und er landet direkt beim Plan
 * (#trainingsplan, siehe Hundeseite).
 *
 * Der Rückweg führt zurück hierher statt zu den eigenen Hunden (?from=).
 */
export function SupervisedDogsSection() {
  const [dogs, setDogs] = useState<SupervisedDog[] | null>(null);

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
          toast.error(err instanceof ApiError ? err.message : "Betreute Hunde konnten nicht geladen werden.");
        }
      });
    return () => {
      active = false;
    };
  }, []);

  // Ohne betreute Hunde nichts anzeigen: eine leere Karte hier hilft nicht -
  // betreut wird über die Gruppe ("Hund betreuen"), nicht von hier aus.
  if (dogs !== null && dogs.length === 0) return null;

  return (
    <Card>
      <CardHeader className="items-center">
        <CardTitle className="flex items-center gap-2 text-base">
          <PawPrint className="size-5" />
          Betreute Hunde
        </CardTitle>
        {dogs && (
          <CardAction>
            <Badge variant="secondary">{dogs.length}</Badge>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {dogs === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : (
          <ul className="flex flex-col gap-1.5">
            {dogs.map((dog) => (
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
                      kein Ziel
                    </Badge>
                  )}
                  <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
