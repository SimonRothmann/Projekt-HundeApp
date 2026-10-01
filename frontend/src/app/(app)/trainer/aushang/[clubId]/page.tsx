"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { einladungsUrl } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import type { Club, ClubInviteLink } from "@/lib/types";
import { AushangAnsicht, AushangKeinInhalt } from "@/components/trainer/aushang-blatt";

type Stand =
  | { art: "laedt" }
  | { art: "bereit"; vereinsname: string; link: string }
  | { art: "keinLink" }
  // Auch "gibt es nicht": Der Server antwortet für fremde Vereine wie für
  // unbekannte mit 404 und verrät nicht, welches von beiden zutrifft.
  | { art: "keinZugriff" }
  | { art: "fehler" };

/**
 * Aushang für das Vereinsheim: QR-Code, drei Schritte, Link als Text.
 *
 * Nur für die, die den Link des Vereins verwalten dürfen - das entscheidet
 * der Server, hier wird nur seine Antwort in eine verständliche Meldung
 * übersetzt. Ohne Berechtigung liefert er den Code gar nicht erst aus.
 *
 * Das Blatt selbst (fest schwarz auf weiß, druckfähig) steht in
 * components/trainer/aushang-blatt.tsx und ist für den Aushang der
 * Gruppenanmeldung dasselbe.
 */
export default function AushangPage() {
  const t = useT();
  const { clubId } = useParams<{ clubId: string }>();
  const [stand, setStand] = useState<Stand>({ art: "laedt" });

  useEffect(() => {
    let abgebrochen = false;
    // Der Code zuerst und allein: Verweigert der Server ihn, gibt es nichts zu zeigen.
    api
      .get<ClubInviteLink | undefined>(`/api/clubs/${clubId}/invite-link`)
      .then(async (antwort) => {
        if (abgebrochen) return;
        if (!antwort) {
          setStand({ art: "keinLink" });
          return;
        }
        // Der Name kommt aus den eigenen Vereinen; wer den Code lesen darf, ist
        // dort Trainer:in und findet den Verein auch in dieser Liste.
        const vereine = await api.get<Club[]>("/api/groups/my-clubs");
        if (abgebrochen) return;
        const verein = vereine.find((v) => v.id === clubId);
        if (!verein) {
          setStand({ art: "keinZugriff" });
          return;
        }
        setStand({ art: "bereit", vereinsname: verein.name, link: einladungsUrl(window.location.origin, antwort.code) });
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
    // t bewusst nicht in der Liste: nur für die Fehlermeldung (siehe dogs/[id]/print).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clubId]);

  if (stand.art === "laedt") return <p className="p-6 text-muted-foreground">{t("Lädt…")}</p>;

  if (stand.art !== "bereit") {
    return (
      <AushangKeinInhalt
        text={
          stand.art === "keinLink"
            ? t("Für diesen Verein gibt es noch keinen Einladungslink. Erstelle ihn zuerst in der Trainer-Übersicht.")
            : stand.art === "keinZugriff"
              ? t("Diesen Aushang kannst du nicht erstellen: Den Verein gibt es nicht, oder du verwaltest ihn nicht.")
              : t("Der Aushang konnte nicht geladen werden. Versuche es gleich noch einmal.")
        }
      />
    );
  }

  return (
    <AushangAnsicht
      seitentitel={t("Aushang für {verein}", { verein: stand.vereinsname })}
      name={stand.vereinsname}
      ueberschrift={t("Trainiere mit uns in Dogity")}
      schritte={[t("QR-Code scannen"), t("Konto erstellen"), t("Der Verein bestätigt deine Anfrage")]}
      link={stand.link}
      qrBeschreibung={t("QR-Code mit dem Einladungslink zu {verein}", { verein: stand.vereinsname })}
    />
  );
}
