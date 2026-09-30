"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Printer } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { einladungsUrl } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import type { Club, ClubInviteLink } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { QrCode } from "@/components/ui/qr-code";

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
 * Das Blatt ist bewusst fest schwarz auf weiß (nicht themenabhängig): Im
 * dunklen Erscheinungsbild druckte sonst helle Schrift auf weißes Papier, und
 * ein QR-Code braucht ohnehin hellen Grund. Auf dem Bildschirm dient es
 * zugleich als Vorschau.
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
      <div className="mx-auto flex max-w-md flex-col gap-4 py-10 text-center">
        <p>
          {stand.art === "keinLink"
            ? t("Für diesen Verein gibt es noch keinen Einladungslink. Erstelle ihn zuerst in der Trainer-Übersicht.")
            : stand.art === "keinZugriff"
              ? t("Diesen Aushang kannst du nicht erstellen: Den Verein gibt es nicht, oder du verwaltest ihn nicht.")
              : t("Der Aushang konnte nicht geladen werden. Versuche es gleich noch einmal.")}
        </p>
        <Link href="/trainer" className="text-primary-text underline-offset-4 hover:underline">
          {t("Zur Trainer-Übersicht")}
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-2xl min-w-0 flex-col gap-6 print:max-w-none">
      <div className="flex flex-wrap items-center justify-between gap-2 print:hidden">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Aushang für {verein}", { verein: stand.vereinsname })}</h1>
        <Button onClick={() => window.print()}>
          <Printer className="size-4" />
          {t("Drucken")}
        </Button>
      </div>

      <section className="flex min-w-0 flex-col items-center gap-6 rounded-xl border bg-white p-6 text-center text-black print:rounded-none print:border-0 print:p-0">
        <p className="text-lg font-semibold [overflow-wrap:anywhere]">{stand.vereinsname}</p>
        <h2 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">{t("Trainiere mit uns in Dogity")}</h2>

        {/* Gedruckt mindestens 10 cm (Scanner brauchen Reserve, wenn der Aushang
            in einigen Metern Abstand hängt); auf dem Bildschirm so groß wie
            der Platz, aber nie breiter als das Blatt - auch bei 375 px. */}
        <QrCode
          wert={stand.link}
          ecc="Q"
          beschreibung={t("QR-Code mit dem Einladungslink zu {verein}", { verein: stand.vereinsname })}
          className="w-72 max-w-full print:w-[10cm]"
        />

        <ol className="flex w-full max-w-md flex-col gap-3 text-left text-lg">
          {[t("QR-Code scannen"), t("Konto erstellen"), t("Der Verein bestätigt deine Anfrage")].map((schritt, index) => (
            <li key={index} className="flex items-start gap-3">
              <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-black font-bold text-white">
                {index + 1}
              </span>
              <span className="min-w-0 pt-0.5 [overflow-wrap:anywhere]">{schritt}</span>
            </li>
          ))}
        </ol>

        <p className="w-full text-sm [overflow-wrap:anywhere]">{stand.link}</p>
      </section>
    </div>
  );
}
