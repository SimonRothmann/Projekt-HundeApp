"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupRegistration } from "@/lib/types";
import { toast } from "sonner";
import { RegistrationAttendance } from "@/components/trainer/registration-attendance";
import { RegistrationEditSheet } from "@/components/trainer/registration-edit-sheet";
import { RegistrationFormCard } from "@/components/trainer/registration-form-card";
import { RegistrationImportSheet } from "@/components/trainer/registration-import-sheet";
import { RegistrationList } from "@/components/trainer/registration-list";

/**
 * Anmeldungen einer Gruppe (Welpengruppe & Co.): Anmeldeformular mit QR-Code,
 * Anwesenheit je Termin und die Liste der Angemeldeten samt "bezahlt".
 *
 * Die Angemeldeten sind Kursteilnehmende ohne Dogity-Konto und keine
 * Gruppenmitglieder. Sehen und verwalten dürfen sie nur die, die die Gruppe
 * verwalten - das entscheidet der Server: Alle anderen bekommen 404, und dann
 * zeigt diese Komponente gar nichts (auch kein "Fehler", die Seite der Gruppe
 * gibt es ja für Mitglieder auch).
 *
 * Eigene Komponente statt noch mehr Zustand in der Gruppenseite; hier liegt die
 * Liste, alles Weitere (Karten, Sheets) bekommt sie von oben.
 */
export function GroupRegistrationsSection({ groupId, groupName }: { groupId: string; groupName: string }) {
  const t = useT();
  const [liste, setListe] = useState<GroupRegistration[] | null>(null);
  const [keinZugriff, setKeinZugriff] = useState(false);
  const [bearbeiten, setBearbeiten] = useState<{ offen: boolean; zeile: GroupRegistration | null }>({ offen: false, zeile: null });
  const [importOffen, setImportOffen] = useState(false);

  const laden = useCallback(async () => {
    try {
      setListe(await api.get<GroupRegistration[]>(`/api/groups/${groupId}/registrations`));
    } catch (err) {
      // 404 = darf die Gruppe nicht verwalten (oder es gibt sie nicht): nichts anzeigen.
      if (err instanceof ApiError && err.status === 404) setKeinZugriff(true);
      else toast.error(err instanceof ApiError ? err.message : t("Anmeldungen konnten nicht geladen werden."));
    }
    // t bewusst nicht in der Liste: nur für die Fehlermeldung.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden]);

  // Erst zeigen, wenn die Liste da ist: Sie ist der Beweis, dass der Server die
  // Rechte bestätigt hat. Die Karte "Anmeldeformular" hat keine eigene
  // Rechteprüfung, sie fragt nur den Link - für Nicht-Trainer:innen wäre das
  // ein Fehler-Toast auf einer Seite, die sie völlig zu Recht sehen.
  if (keinZugriff || liste === null) return null;

  function ersetzen(id: string, neu: GroupRegistration | null) {
    setListe((alt) => (alt ? (neu ? alt.map((r) => (r.id === id ? neu : r)) : alt.filter((r) => r.id !== id)) : alt));
  }

  function anzahlAendern(id: string, delta: 1 | -1) {
    setListe((alt) => alt?.map((r) => (r.id === id ? { ...r, attendanceCount: Math.max(0, r.attendanceCount + delta) } : r)) ?? alt);
  }

  return (
    <>
      <RegistrationFormCard groupId={groupId} groupName={groupName} />
      <RegistrationAttendance groupId={groupId} registrierungen={liste} onAnzahlGeaendert={anzahlAendern} />
      <RegistrationList
        groupId={groupId}
        registrierungen={liste}
        onGeaendert={ersetzen}
        onNeu={() => setBearbeiten({ offen: true, zeile: null })}
        onBearbeiten={(zeile) => setBearbeiten({ offen: true, zeile })}
        onImport={() => setImportOffen(true)}
      />
      <RegistrationEditSheet
        groupId={groupId}
        registrierung={bearbeiten.zeile}
        offen={bearbeiten.offen}
        onOffenChange={(offen) => setBearbeiten((b) => ({ ...b, offen }))}
        onGespeichert={laden}
      />
      <RegistrationImportSheet
        groupId={groupId}
        bestehende={liste}
        offen={importOffen}
        onOffenChange={setImportOffen}
        onImportiert={laden}
      />
    </>
  );
}
