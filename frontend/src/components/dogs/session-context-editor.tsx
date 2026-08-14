"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GeocodeResult, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Clock, Crosshair, MapPin, Search } from "lucide-react";
import { toast } from "sonner";
import { formatTemperature, weatherIcon, weatherLabel } from "@/lib/weather";

/**
 * Uhrzeit + Ort eines Trainings setzen - Grundlage der automatischen
 * Wetter-Ermittlung (das Wetter selbst holt der Server, siehe
 * WeatherEnrichmentService).
 *
 * Beides ist sowohl per Knopf (jetzt / aktueller Standort) als auch von Hand
 * setzbar, weil Trainings häufig nachgetragen werden - dann stimmen "jetzt"
 * und "hier" gerade nicht.
 */
export function SessionContextEditor({
  session,
  onSaved,
}: {
  session: TrainingSession;
  onSaved: () => Promise<void> | void;
}) {
  const [open, setOpen] = useState(false);
  // "HH:mm" fürs Zeit-Input; Backend liefert "HH:mm:ss".
  const [time, setTime] = useState(session.startTime?.slice(0, 5) ?? "");
  const [latitude, setLatitude] = useState<number | null>(session.latitude);
  const [longitude, setLongitude] = useState<number | null>(session.longitude);
  const [locationName, setLocationName] = useState(session.locationName ?? "");
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<GeocodeResult[] | null>(null);
  const [saving, setSaving] = useState(false);
  const [locating, setLocating] = useState(false);

  const weather =
    session.temperatureC != null
      ? `${weatherIcon(session.weatherCode)} ${formatTemperature(session.temperatureC)}${
          weatherLabel(session.weatherCode) ? ` · ${weatherLabel(session.weatherCode)}` : ""
        }`
      : null;

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      toast.error("Standort wird von diesem Gerät nicht unterstützt.");
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLatitude(pos.coords.latitude);
        setLongitude(pos.coords.longitude);
        setLocationName("Aktueller Standort");
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

  async function searchLocation() {
    if (!query.trim()) return;
    try {
      setResults(await api.get<GeocodeResult[]>(`/api/weather/locations?query=${encodeURIComponent(query.trim())}`));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ortssuche fehlgeschlagen.");
    }
  }

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/trainings/${session.id}/context`, {
        // Backend erwartet TimeOnly; leer = nicht gesetzt.
        startTime: time ? `${time}:00` : null,
        latitude,
        longitude,
        locationName: locationName.trim() || null,
      });
      toast.success("Ort & Zeit gespeichert – Wetter wird ermittelt.");
      setOpen(false);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  if (!open) {
    return (
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
        {weather && <span className="font-medium text-foreground">{weather}</span>}
        {session.startTime && (
          <span className="flex items-center gap-1">
            <Clock className="size-3" />
            {session.startTime.slice(0, 5)}
          </span>
        )}
        {session.locationName && (
          <span className="flex items-center gap-1 [overflow-wrap:anywhere]">
            <MapPin className="size-3" />
            {session.locationName}
          </span>
        )}
        <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-xs" onClick={() => setOpen(true)}>
          {weather ? "Ort & Zeit ändern" : "Ort & Zeit für Wetter"}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`time-${session.id}`}>Uhrzeit des Trainings</Label>
        <div className="flex gap-2">
          <Input
            id={`time-${session.id}`}
            type="time"
            value={time}
            onChange={(e) => setTime(e.target.value)}
            className="flex-1"
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setTime(new Date().toTimeString().slice(0, 5))}
          >
            <Clock className="size-3.5" />
            Jetzt
          </Button>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label>Trainingsort</Label>
        <Button type="button" variant="outline" size="sm" disabled={locating} onClick={useCurrentLocation}>
          <Crosshair className="size-3.5" />
          {locating ? "Ermittle…" : "Aktuellen Standort verwenden"}
        </Button>
        <div className="flex gap-2">
          <Input
            placeholder="oder Ort suchen (z.B. Musterstadt)"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                searchLocation();
              }
            }}
          />
          <Button type="button" variant="outline" size="sm" onClick={searchLocation}>
            <Search className="size-3.5" />
          </Button>
        </div>
        {results !== null && (
          <ul className="flex flex-col gap-1">
            {results.length === 0 ? (
              <li className="text-xs text-muted-foreground">Kein Ort gefunden.</li>
            ) : (
              results.map((r, i) => (
                <li key={i}>
                  <button
                    type="button"
                    className="w-full rounded-md border px-2 py-1.5 text-left text-sm transition-colors hover:bg-muted coarse:min-h-11"
                    onClick={() => {
                      setLatitude(r.latitude);
                      setLongitude(r.longitude);
                      setLocationName([r.name, r.region, r.country].filter(Boolean).join(", "));
                      setResults(null);
                      setQuery("");
                    }}
                  >
                    {r.name}
                    {r.region ? `, ${r.region}` : ""}
                    {r.country ? ` (${r.country})` : ""}
                  </button>
                </li>
              ))
            )}
          </ul>
        )}
        {latitude != null && longitude != null && (
          <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
            Gewählt: {locationName || `${latitude.toFixed(4)}, ${longitude.toFixed(4)}`}
          </p>
        )}
      </div>

      <p className="text-xs text-muted-foreground">
        Mit Ort und Uhrzeit wird das Wetter automatisch ermittelt – auch für Trainings, die du nachträgst.
      </p>

      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={saving} onClick={save}>
          {saving ? "Speichert…" : "Speichern"}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={() => setOpen(false)}>
          Abbrechen
        </Button>
      </div>
    </div>
  );
}
