"use client";

import { anmeldungsUrl } from "@/lib/anmeldung";
import { useT } from "@/lib/i18n";
import { LinkVerwaltung } from "@/components/trainer/link-verwaltung";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Anmeldelink und QR-Code einer Gruppe in einem Sheet: Kursteilnehmende ohne
 * Dogity-Konto melden sich damit an (z. B. zur Welpengruppe).
 *
 * Früher eine große Karte oben auf der Gruppenseite - den QR-Code braucht man
 * aber nur zum Zeigen oder Teilen, nicht bei jedem Besuch. Der Link wird erst
 * geladen, wenn das Sheet aufgeht. Und er bleibt für jede:n erreichbar, der die
 * Anmeldungen sieht: Gebraucht wird er gerade, wenn noch niemand angemeldet ist.
 */
export function RegistrationFormSheet({
  groupId,
  groupName,
  offen,
  onOffenChange,
}: {
  groupId: string;
  groupName: string;
  offen: boolean;
  onOffenChange: (offen: boolean) => void;
}) {
  const t = useT();
  return (
    <Sheet open={offen} onOpenChange={onOffenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Anmeldelink & QR-Code")}</SheetTitle>
          <SheetDescription>
            {t("Wer den Code scannt oder den Link öffnet, kann sich ohne Dogity-Konto zur Gruppe anmelden. Die Anmeldungen erscheinen in der Liste.")}
          </SheetDescription>
        </SheetHeader>
        <Inhalt groupId={groupId} groupName={groupName} />
      </BottomSheetContent>
    </Sheet>
  );
}

function Inhalt({ groupId, groupName }: { groupId: string; groupName: string }) {
  const t = useT();

  return (
    <div className="flex min-w-0 flex-col gap-4 px-4">
      <LinkVerwaltung
        pfad={`/api/groups/${groupId}/registration-link`}
        linkBauen={anmeldungsUrl}
        aushangPfad={`/trainer/aushang/gruppe/${groupId}`}
        texte={{
          erstellen: t("Anmeldeformular erstellen"),
          ladenFehler: t("Anmeldeformular konnte nicht geladen werden."),
          erstellenFehler: t("Anmeldeformular konnte nicht erstellt werden."),
          abschaltenFrage: t("Formular schließen? Der Link und der QR-Code funktionieren danach nicht mehr. Die bisherigen Anmeldungen bleiben."),
          abschalten: t("Formular schließen"),
          abschaltenErfolg: t("Anmeldeformular geschlossen."),
          abschaltenFehler: t("Anmeldeformular konnte nicht geschlossen werden."),
          qrBeschreibung: t("QR-Code mit dem Anmeldelink zu {gruppe}", { gruppe: groupName }),
          teilenTitel: t("Anmeldung zur {gruppe}", { gruppe: groupName }),
          teilenText: t("Hier kannst du dich zur {gruppe} anmelden.", { gruppe: groupName }),
        }}
      />
    </div>
  );
}
