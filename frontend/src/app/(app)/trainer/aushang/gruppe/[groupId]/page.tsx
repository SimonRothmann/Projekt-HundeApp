"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { anmeldungsUrl } from "@/lib/anmeldung";
import { useT } from "@/lib/i18n";
import type { GroupRegistrationForm, GroupRegistrationLink } from "@/lib/types";
import { AushangAnsicht, AushangKeinInhalt } from "@/components/trainer/aushang-blatt";

type Stand =
  | { art: "laedt" }
  | { art: "bereit"; name: string; gruppenname: string; link: string }
  | { art: "keinLink" }
  // Auch "gibt es nicht": Der Server antwortet für fremde Gruppen wie für
  // unbekannte mit 404 und verrät nicht, welches von beiden zutrifft.
  | { art: "keinZugriff" }
  | { art: "fehler" };

/**
 * Aushang für das Anmeldeformular einer Gruppe, z. B. der Welpengruppe: QR-Code,
 * drei Schritte, Link als Text. Dasselbe Blatt wie beim Vereinsaushang
 * (components/trainer/aushang-blatt.tsx).
 *
 * Der Code kommt nur, wenn der Server die Aufrufer:in die Gruppe verwalten
 * lässt; hier wird nur seine Antwort in eine verständliche Meldung übersetzt.
 * Die Namen auf dem Blatt kommen aus der öffentlichen Vorschau des Codes - also
 * genau das, was auch die Angemeldeten später sehen.
 */
export default function GruppenAushangPage() {
  const t = useT();
  const { groupId } = useParams<{ groupId: string }>();
  const [stand, setStand] = useState<Stand>({ art: "laedt" });

  useEffect(() => {
    let abgebrochen = false;
    api
      // Ohne Link antwortet der Server mit 204 - die api-Hülle macht daraus undefined.
      .get<GroupRegistrationLink | undefined>(`/api/groups/${groupId}/registration-link`)
      .then(async (antwort) => {
        if (abgebrochen) return;
        if (!antwort) {
          setStand({ art: "keinLink" });
          return;
        }
        const vorschau = await api.get<GroupRegistrationForm>(`/api/groups/registration/${antwort.code}`);
        if (abgebrochen) return;
        setStand({
          art: "bereit",
          name: vorschau.clubName ?? vorschau.groupName,
          gruppenname: vorschau.groupName,
          link: anmeldungsUrl(window.location.origin, antwort.code),
        });
      })
      .catch((err) => {
        if (abgebrochen) return;
        if (err instanceof ApiError && err.status === 404) {
          setStand({ art: "keinZugriff" });
          return;
        }
        toast.error(err instanceof ApiError ? err.message : t("Der Aushang konnte nicht geladen werden."));
        setStand({ art: "fehler" });
      });
    return () => {
      abgebrochen = true;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung (siehe aushang/[clubId]).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  if (stand.art === "laedt") return <p className="p-6 text-muted-foreground">{t("Lädt…")}</p>;

  if (stand.art !== "bereit") {
    return (
      <AushangKeinInhalt
        text={
          stand.art === "keinLink"
            ? t("Für diese Gruppe gibt es noch kein Anmeldeformular. Erstelle es zuerst auf der Seite der Gruppe.")
            : stand.art === "keinZugriff"
              ? t("Diesen Aushang kannst du nicht erstellen: Die Gruppe gibt es nicht, oder du verwaltest sie nicht.")
              : t("Der Aushang konnte nicht geladen werden. Versuche es gleich noch einmal.")
        }
      />
    );
  }

  return (
    <AushangAnsicht
      seitentitel={t("Aushang für {gruppe}", { gruppe: stand.gruppenname })}
      name={stand.name}
      ueberschrift={t("Anmeldung zur {gruppe}", { gruppe: stand.gruppenname })}
      schritte={[t("QR-Code scannen"), t("Formular ausfüllen"), t("Der Verein meldet sich bei dir")]}
      link={stand.link}
      qrBeschreibung={t("QR-Code mit dem Anmeldelink zu {gruppe}", { gruppe: stand.gruppenname })}
    />
  );
}
