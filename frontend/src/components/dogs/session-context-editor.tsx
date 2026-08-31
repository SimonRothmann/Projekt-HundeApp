"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Clock, MapPin } from "lucide-react";
import { toast } from "sonner";
import { formatTemperature, weatherIcon, weatherLabel } from "@/lib/weather";
import { LocationTimeFields, type LocationValue } from "@/components/dogs/location-time-fields";

/**
 * Uhrzeit + Ort eines bereits erfassten Trainings nachträglich ändern.
 *
 * Die Eingabefelder selbst stecken in LocationTimeFields - dieselbe Eingabe
 * benutzt das Formular "Neues Training". Hier drumherum liegen nur die
 * eingeklappte Zusammenfassung (mit dem ermittelten Wetter) und das
 * Speichern.
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
  const [location, setLocation] = useState<LocationValue>({
    latitude: session.latitude,
    longitude: session.longitude,
    locationName: session.locationName ?? "",
  });
  const [saving, setSaving] = useState(false);

  const weather =
    session.temperatureC != null
      ? `${weatherIcon(session.weatherCode)} ${formatTemperature(session.temperatureC)}${
          weatherLabel(session.weatherCode) ? ` · ${weatherLabel(session.weatherCode)}` : ""
        }`
      : null;

  /**
   * Formular beim Öffnen frisch aus der Trainingseinheit füllen.
   *
   * Zwingend nötig, weil die Hundeseite erst aus dem Lesecache rendert und
   * die Netzantwort nachreicht: useState übernimmt den Wert nur beim ersten
   * Rendern, das Formular hinge sonst auf dem Stand von vorhin. Wer dann
   * speichert, schreibt den veralteten Ort zurück und überschreibt eine
   * neuere Eingabe. Im Handler statt im Effekt, damit nichts überschrieben
   * wird, während jemand tippt.
   */
  function openEditor() {
    setTime(session.startTime?.slice(0, 5) ?? "");
    setLocation({
      latitude: session.latitude,
      longitude: session.longitude,
      locationName: session.locationName ?? "",
    });
    setOpen(true);
  }

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/trainings/${session.id}/context`, {
        // Backend erwartet TimeOnly; leer = nicht gesetzt.
        startTime: time ? `${time}:00` : null,
        latitude: location.latitude,
        longitude: location.longitude,
        locationName: location.locationName.trim() || null,
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
        <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-xs" onClick={openEditor}>
          {weather ? "Ort & Zeit ändern" : "Ort & Zeit für Wetter"}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-3 rounded-md border p-3">
      <LocationTimeFields
        idPrefix={session.id}
        time={time}
        onTimeChange={setTime}
        location={location}
        onLocationChange={setLocation}
      />

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
