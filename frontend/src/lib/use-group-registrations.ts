"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupRegistration } from "@/lib/types";

/**
 * Die Anmeldungen einer Gruppe, einmal geladen für die ganze Seite.
 *
 * Die Gruppenseite braucht Anzahl und Zugriff schon für den Umschalter
 * "Mitglieder / Anmeldungen" - nicht erst, wenn die Ansicht offen ist. Deshalb
 * liegt das Laden hier und nicht in der Ansicht selbst, und nichts wird
 * doppelt abgefragt.
 *
 * Sehen und verwalten dürfen die Anmeldungen nur die, die die Gruppe
 * verwalten - das entscheidet der Server: Alle anderen bekommen 404, und dann
 * steht `keinZugriff` auf true und die Seite zeigt weder Umschalter noch
 * Anmeldungen (auch kein "Fehler": die Seite der Gruppe gibt es für
 * Mitglieder ja auch).
 */
export function useGroupRegistrations(groupId: string) {
  const t = useT();
  const [liste, setListe] = useState<GroupRegistration[] | null>(null);
  const [keinZugriff, setKeinZugriff] = useState(false);
  // Ein anderer Fehler als 404 (Netz, 500): Toast, aber kein "kein Zugriff" - die Liste bleibt null.
  const [fehler, setFehler] = useState(false);

  const laden = useCallback(async () => {
    try {
      setListe(await api.get<GroupRegistration[]>(`/api/groups/${groupId}/registrations`));
      setFehler(false);
    } catch (err) {
      // 404 = darf die Gruppe nicht verwalten (oder es gibt sie nicht): nichts anzeigen.
      if (err instanceof ApiError && err.status === 404) setKeinZugriff(true);
      else {
        setFehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Anmeldungen konnten nicht geladen werden."));
      }
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  function ersetzen(id: string, neu: GroupRegistration | null) {
    setListe((alt) => (alt ? (neu ? alt.map((r) => (r.id === id ? neu : r)) : alt.filter((r) => r.id !== id)) : alt));
  }

  function anzahlAendern(id: string, delta: 1 | -1) {
    setListe((alt) => alt?.map((r) => (r.id === id ? { ...r, attendanceCount: Math.max(0, r.attendanceCount + delta) } : r)) ?? alt);
  }

  return {
    /** Erst da, wenn der Server die Rechte bestätigt hat - sie ist der Beweis dafür. */
    liste,
    keinZugriff,
    /** Die Antwort steht noch aus: weder Liste noch 404 noch Fehler. */
    wirdGeladen: liste === null && !keinZugriff && !fehler,
    /** Laden ist gescheitert (nicht 404) - die Seite bietet "Erneut versuchen" an. */
    fehler,
    laden,
    ersetzen,
    anzahlAendern,
  };
}

export type GruppenAnmeldungen = ReturnType<typeof useGroupRegistrations>;
