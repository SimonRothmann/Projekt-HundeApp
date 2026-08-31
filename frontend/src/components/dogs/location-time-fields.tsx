"use client";

import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import type { GeocodeResult, RecentLocation } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Clock, Crosshair, History, Search } from "lucide-react";
import { toast } from "sonner";

/** Kürzere Eingaben liefern nur Rauschen und kosten unnötig Anfragen. */
const SEARCH_MIN_CHARS = 3;
const SEARCH_DEBOUNCE_MS = 350;

export type LocationValue = {
  latitude: number | null;
  longitude: number | null;
  locationName: string;
};

/**
 * Uhrzeit + Ort eines Trainings eingeben - Grundlage der automatischen
 * Wetter-Ermittlung (das Wetter selbst holt der Server, siehe
 * WeatherEnrichmentService).
 *
 * Drei Wege zum Ort, absteigend nach Häufigkeit im Alltag:
 * 1. "Zuletzt" - ein Tipp. Hundeführer trainieren fast immer an denselben
 *    zwei bis fünf Plätzen, das ist der Normalfall.
 * 2. Aktueller Standort - wenn man gerade dort steht.
 * 3. Suche oder freier Name - beim ersten Mal, oder wenn der Platz in
 *    OpenStreetMap gar nicht steht.
 *
 * Der Name bleibt IMMER von Hand änderbar: viele Hundeplätze sind unbenannt
 * oder heißen offiziell anders, als man sie nennt.
 *
 * Rein gesteuert (Werte kommen von außen, kein eigenes Speichern): so nutzen
 * das nachträgliche Ändern im Tagebuch (SessionContextEditor) und das
 * Erfassen eines neuen Trainings (TrainingForm) dieselbe Eingabe. Vorher gab
 * es sie nur im Tagebuch - beim Erfassen ließen sich Ort und Zeit gar nicht
 * angeben, obwohl der Server sie von Anfang an entgegennimmt.
 */
export function LocationTimeFields({
  idPrefix,
  time,
  onTimeChange,
  location,
  onLocationChange,
}: {
  idPrefix: string;
  time: string;
  onTimeChange: (value: string) => void;
  location: LocationValue;
  onLocationChange: (value: LocationValue) => void;
}) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<GeocodeResult[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [recent, setRecent] = useState<RecentLocation[]>([]);
  const [locating, setLocating] = useState(false);

  // Verhindert, dass eine langsame ältere Antwort eine neuere überschreibt.
  const searchSeq = useRef(0);

  useEffect(() => {
    let active = true;
    api
      .get<RecentLocation[]>("/api/trainings/locations")
      .then((list) => {
        // Ältere Backends kennen den Endpunkt nicht - dann bleibt die
        // Schnellauswahl einfach leer, statt die Eingabe zu blockieren.
        if (active) setRecent(list ?? []);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, []);

  // Tipp-Suche: Photon findet auch bei angefangenen Wörtern etwas. Der Effekt
  // plant nur die Anfrage - das Zurücksetzen passiert im Eingabe-Handler.
  useEffect(() => {
    const term = query.trim();
    if (term.length < SEARCH_MIN_CHARS) return;

    const seq = ++searchSeq.current;
    const timer = setTimeout(async () => {
      // Treffer auf die Umgebung gewichten: "Hundeplatz" gibt es hundertfach.
      // Anker ist der bereits gewählte Ort, sonst der zuletzt genutzte - beides
      // ohne zusätzliche Standort-Abfrage beim Nutzer.
      const anchor =
        location.latitude != null && location.longitude != null
          ? { lat: location.latitude, lon: location.longitude }
          : recent[0]
            ? { lat: recent[0].latitude, lon: recent[0].longitude }
            : null;

      const params = new URLSearchParams({ query: term });
      if (anchor) {
        params.set("lat", String(anchor.lat));
        params.set("lon", String(anchor.lon));
      }

      try {
        const found = await api.get<GeocodeResult[]>(`/api/weather/locations?${params}`);
        if (seq === searchSeq.current) setResults(found ?? []);
      } catch {
        if (seq === searchSeq.current) setResults([]);
      } finally {
        if (seq === searchSeq.current) setSearching(false);
      }
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(timer);
    // location/recent wirken nur als Anker - eine Änderung soll keine neue
    // Suche auslösen, deshalb bewusst nicht in den Abhängigkeiten.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query]);

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      toast.error("Standort wird von diesem Gerät nicht unterstützt.");
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        // Namen zu den Koordinaten holen. Ohne das hieße jeder so gesetzte Ort
        // "Aktueller Standort" - in der Liste der zuletzt genutzten Orte
        // fielen sie zu einem Knopf zusammen, der auf die zuletzt
        // gespeicherten Koordinaten zeigt. Nur vorbelegen, wenn noch nichts
        // dasteht; ein selbst vergebener Name bleibt unangetastet.
        let suggestion: string | null = null;
        try {
          const params = new URLSearchParams({ lat: String(pos.coords.latitude), lon: String(pos.coords.longitude) });
          suggestion = (await api.get<GeocodeResult | null>(`/api/weather/locations/reverse?${params}`))?.name ?? null;
        } catch {
          // Reiner Komfort - schlägt es fehl, tippt man den Namen eben selbst.
        }
        onLocationChange({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
          locationName: location.locationName || suggestion || "Aktueller Standort",
        });
        setLocating(false);
        toast.success("Standort übernommen.");
      },
      () => {
        setLocating(false);
        toast.error("Standort konnte nicht ermittelt werden.");
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  function onQueryChange(value: string) {
    setQuery(value);
    const searchable = value.trim().length >= SEARCH_MIN_CHARS;
    setSearching(searchable);
    if (!searchable) setResults(null);
  }

  function pick(name: string, lat: number, lon: number) {
    onLocationChange({ latitude: lat, longitude: lon, locationName: name });
    setResults(null);
    setQuery("");
  }

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`time-${idPrefix}`}>Uhrzeit des Trainings</Label>
        <div className="flex gap-2">
          <Input
            id={`time-${idPrefix}`}
            type="time"
            value={time}
            onChange={(e) => onTimeChange(e.target.value)}
            className="min-w-0 flex-1"
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => onTimeChange(new Date().toTimeString().slice(0, 5))}
          >
            <Clock className="size-3.5" />
            Jetzt
          </Button>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`place-${idPrefix}`}>Trainingsort</Label>

        {recent.length > 0 && (
          <div className="flex flex-col gap-1">
            <span className="flex items-center gap-1 text-xs text-muted-foreground">
              <History className="size-3" />
              Zuletzt
            </span>
            <div className="flex flex-wrap gap-1.5">
              {recent.map((r) => (
                <button
                  key={r.name}
                  type="button"
                  className="max-w-full truncate rounded-full border px-3 py-1.5 text-xs transition-colors hover:bg-muted coarse:min-h-11"
                  onClick={() => pick(r.name, r.latitude, r.longitude)}
                >
                  {r.name}
                </button>
              ))}
            </div>
          </div>
        )}

        <Input
          id={`place-${idPrefix}`}
          placeholder="Name des Orts, z.B. Hundeplatz SV OG …"
          value={location.locationName}
          onChange={(e) => onLocationChange({ ...location, locationName: e.target.value })}
        />

        <Button type="button" variant="outline" size="sm" disabled={locating} onClick={useCurrentLocation}>
          <Crosshair className="size-3.5" />
          {locating ? "Ermittle…" : "Aktuellen Standort verwenden"}
        </Button>

        <div className="flex gap-2">
          <Input
            placeholder="oder suchen: Hundeplatz, Verein, Adresse"
            value={query}
            onChange={(e) => onQueryChange(e.target.value)}
            className="min-w-0 flex-1"
          />
          <span className="flex w-9 items-center justify-center text-muted-foreground">
            <Search className={searching ? "size-4 animate-pulse" : "size-4"} />
          </span>
        </div>

        {results !== null && (
          <ul className="flex flex-col gap-1">
            {results.length === 0 ? (
              <li className="text-xs text-muted-foreground">
                {searching ? "Suche…" : "Nichts gefunden – du kannst den Namen oben einfach eintippen."}
              </li>
            ) : (
              results.map((r, i) => (
                <li key={`${r.latitude},${r.longitude},${i}`}>
                  <button
                    type="button"
                    className="w-full rounded-md border px-2 py-1.5 text-left transition-colors hover:bg-muted coarse:min-h-11"
                    onClick={() => pick(r.name, r.latitude, r.longitude)}
                  >
                    <span className="block text-sm [overflow-wrap:anywhere]">{r.name}</span>
                    {r.detail && (
                      <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{r.detail}</span>
                    )}
                  </button>
                </li>
              ))
            )}
          </ul>
        )}

        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {location.latitude != null && location.longitude != null
            ? `Koordinaten gesetzt (${location.latitude.toFixed(3)}, ${location.longitude.toFixed(3)})`
            : "Ohne Koordinaten wird kein Wetter ermittelt – nutze „Aktuellen Standort“ oder die Suche."}
        </p>
      </div>
    </>
  );
}
