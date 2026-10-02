"use client";

import { useState } from "react";
import { FileUp, Plus, QrCode as QrIcon } from "lucide-react";
import type { GroupRegistration } from "@/lib/types";
import { useT } from "@/lib/i18n";
import type { GruppenAnmeldungen } from "@/lib/use-group-registrations";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { RegistrationAttendance } from "@/components/trainer/registration-attendance";
import { RegistrationEditSheet } from "@/components/trainer/registration-edit-sheet";
import { RegistrationFormSheet } from "@/components/trainer/registration-form-sheet";
import { RegistrationImportSheet } from "@/components/trainer/registration-import-sheet";
import { RegistrationList } from "@/components/trainer/registration-list";

/**
 * Ansicht "Anmeldungen" einer Gruppe (Welpengruppe & Co.): Anwesenheit je
 * Termin, die Liste der Angemeldeten samt "bezahlt", dazu Anmeldelink mit
 * QR-Code im Sheet.
 *
 * Die Angemeldeten sind Kursteilnehmende ohne Dogity-Konto und keine
 * Gruppenmitglieder.
 *
 * Die Rechteprüfung liegt nicht hier, sondern vor dem Aufruf: Die Gruppenseite
 * zeigt diese Ansicht erst, wenn die Liste da ist (useGroupRegistrations) -
 * sie ist der Beweis, dass der Server die Rechte bestätigt hat. Der Link
 * zum Anmeldeformular (RegistrationFormSheet) hat keine eigene Rechteprüfung,
 * er fragt nur den Link; für Nicht-Trainer:innen wäre das ein Fehler-Toast
 * auf einer Seite, die sie völlig zu Recht sehen.
 *
 * Ohne Anmeldungen bleibt statt Anwesenheit und Liste ein leerer Zustand mit
 * den Wegen, die ersten hereinzubekommen.
 */
export function GroupRegistrationsSection({
  groupId,
  groupName,
  liste,
  laden,
  ersetzen,
  anzahlAendern,
}: {
  groupId: string;
  groupName: string;
  liste: GroupRegistration[];
  laden: GruppenAnmeldungen["laden"];
  ersetzen: GruppenAnmeldungen["ersetzen"];
  anzahlAendern: GruppenAnmeldungen["anzahlAendern"];
}) {
  const t = useT();
  const [bearbeiten, setBearbeiten] = useState<{ offen: boolean; zeile: GroupRegistration | null }>({ offen: false, zeile: null });
  const [importOffen, setImportOffen] = useState(false);
  const [linkOffen, setLinkOffen] = useState(false);

  return (
    <>
      {liste.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col gap-4 py-6">
            <div className="flex flex-col gap-1 text-center">
              <p className="font-medium">{t("Noch keine Anmeldungen.")}</p>
              <p className="text-sm text-muted-foreground">
                {t("Teile den Anmeldelink oder zeige den QR-Code. Anmeldungen lassen sich auch von Hand eintragen oder aus einem Google-Formular übernehmen.")}
              </p>
            </div>
            <div className="flex flex-col gap-2">
              <Button className="h-11" onClick={() => setLinkOffen(true)}>
                <QrIcon className="size-4" />
                {t("Anmeldelink & QR-Code")}
              </Button>
              <Button variant="outline" className="h-11" onClick={() => setBearbeiten({ offen: true, zeile: null })}>
                <Plus className="size-4" />
                {t("Anmeldung hinzufügen")}
              </Button>
              <Button variant="outline" className="h-11" onClick={() => setImportOffen(true)}>
                <FileUp className="size-4" />
                {t("Aus Google-Formular importieren")}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : (
        <>
          <RegistrationAttendance groupId={groupId} registrierungen={liste} onAnzahlGeaendert={anzahlAendern} />
          <RegistrationList
            groupId={groupId}
            registrierungen={liste}
            onGeaendert={ersetzen}
            onNeu={() => setBearbeiten({ offen: true, zeile: null })}
            onBearbeiten={(zeile) => setBearbeiten({ offen: true, zeile })}
            onImport={() => setImportOffen(true)}
            onLink={() => setLinkOffen(true)}
          />
        </>
      )}
      <RegistrationFormSheet groupId={groupId} groupName={groupName} offen={linkOffen} onOffenChange={setLinkOffen} />
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
