"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { Dog as DogIcon } from "lucide-react";

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
      const cached = await getCachedData<string>(key);
      if (cached && active) setLoaded(cached);

      try {
        // 204 ohne Bild - dann liefert der Client undefined, kein Fehler.
        const fresh = await api.get<{ dataUrl: string } | undefined>(`/api/dogs/${dogId}/image`);
        if (!active) return;
        if (fresh?.dataUrl) {
          setLoaded(fresh.dataUrl);
          await setCachedData(key, fresh.dataUrl);
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
