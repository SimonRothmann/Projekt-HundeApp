"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { Dog as DogIcon } from "lucide-react";

/**
 * Das Bild liegt zusammen mit seinem Kennzeichen im Cache. Ältere Einträge
 * enthalten nur die Data-URI als Zeichenkette - die bleiben gültig, sie
 * führen lediglich einmal zu einem vollen statt einem bedingten Abruf.
 */
type GecachtesBild = { dataUrl: string; etag: string | null };

async function gecachtesBild(key: string): Promise<GecachtesBild | null> {
  const eintrag = await getCachedData<GecachtesBild | string>(key);
  if (!eintrag) return null;
  return typeof eintrag === "string" ? { dataUrl: eintrag, etag: null } : eintrag;
}

/**
 * Profilbild eines Hundes - oder das Platzhalter-Symbol, solange keines
 * hinterlegt ist.
 *
 * Das Bild kommt über einen eigenen Aufruf und nicht mit dem Hund mit: sonst
 * hinge an jeder Hundeliste - und an der Trainerübersicht mit vielen betreuten
 * Hunden - das vollständige Bildmaterial, obwohl dort nur Namen stehen.
 *
 * Einmal geholt, landet es im Lesecache. Das spart nicht nur den erneuten
 * Abruf, sondern lässt das Bild auch offline stehen bleiben - eine Hundeliste,
 * die ohne Netz plötzlich lauter graue Kreise zeigt, sähe kaputt aus.
 */
export function DogAvatar({
  dogId,
  hasImage,
  name,
  className = "size-12",
  iconClassName = "size-6",
}: {
  dogId: string;
  hasImage: boolean;
  name: string;
  className?: string;
  iconClassName?: string;
}) {
  const [loaded, setLoaded] = useState<string | null>(null);
  // Abgeleitet statt im Effekt zurückgesetzt: fällt hasImage weg (Bild
  // entfernt), verschwindet damit auch das angezeigte Bild sofort.
  const src = hasImage ? loaded : null;

  useEffect(() => {
    if (!hasImage) return;

    let active = true;
    const key = `dog-image-${dogId}`;

    (async () => {
      const cached = await gecachtesBild(key);
      if (cached && active) setLoaded(cached.dataUrl);

      try {
        // Bedingter Abruf: Liegt das Bild schon vor, schickt der Client sein
        // Kennzeichen mit und der Server antwortet mit 304 ohne Rumpf. Das
        // spart die rund 64 KB, die ein Profilbild als Data-URI wiegt - und
        // zwar bei JEDEM Aufbau jeder Liste, in der der Hund vorkommt.
        // Zwischenspeichern kann der Browser die Antwort nicht selbst: eigene
        // Herkunft, Authorization-Kopfzeile, JSON-Rumpf.
        const antwort = await api.getConditional<{ dataUrl: string }>(
          `/api/dogs/${dogId}/image`,
          cached?.etag ?? null,
        );
        if (!active || antwort.art !== "neu") return;
        if (antwort.daten?.dataUrl) {
          setLoaded(antwort.daten.dataUrl);
          await setCachedData(key, { dataUrl: antwort.daten.dataUrl, etag: antwort.etag });
        }
      } catch {
        // Offline oder Serverfehler: der zwischengespeicherte Stand bleibt
        // stehen, sonst das Platzhalter-Symbol. Beides besser als ein Fehler
        // für ein Profilbild.
      }
    })();

    return () => {
      active = false;
    };
  }, [dogId, hasImage]);

  return (
    <div
      className={`flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-secondary ${className}`}
    >
      {src ? (
        // eslint-disable-next-line @next/next/no-img-element -- Data-URI aus der API, next/image bringt hier nichts.
        <img src={src} alt={`Profilbild von ${name}`} className="size-full object-cover" />
      ) : (
        <DogIcon className={`${iconClassName} text-secondary-foreground`} />
      )}
    </div>
  );
}
