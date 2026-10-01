import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { useT } from "@/lib/i18n";
import type { ClubMembership } from "@/lib/types";

/**
 * Einladungslink eines Vereins (Adresse /v/{code}, dazu der QR-Code am
 * Vereinsheim).
 *
 * Der Code kommt aus der Adresszeile und damit von außen. Bevor er in eine
 * Adresse oder an die API geht, wird seine Form geprüft - alles andere wird
 * ignoriert, nicht repariert: Ein unbrauchbarer Wert soll nie zu einem
 * Sprung auf eine fremde Seite oder zu einem seltsamen Pfad werden.
 */

/**
 * Das Backend erzeugt 22 Zeichen (128 Bit, URL-sicher). Die Obergrenze liegt
 * bewusst darüber (die Spalte fasst 32), damit eine spätere Verlängerung des
 * Codes nicht zuerst das Frontend aussperrt.
 */
const CODE_FORM = /^[A-Za-z0-9_-]{1,32}$/;

export function istGueltigerEinladungscode(wert: string | null | undefined): wert is string {
  return typeof wert === "string" && CODE_FORM.test(wert);
}

/** Der Code aus einem Parameter, oder null, wenn er fehlt oder unbrauchbar ist. */
export function einladungscodeAus(wert: string | null | undefined): string | null {
  return istGueltigerEinladungscode(wert) ? wert : null;
}

/** Die Adresse, die in den QR-Code und in den Link kommt. */
export function einladungsUrl(origin: string, code: string): string {
  return `${origin}/v/${code}`;
}

/**
 * Stellt die Beitrittsanfrage über den Link und meldet das Ergebnis mit einer
 * Meldung. True, wenn die Anfrage durchging (oder die Person schon Mitglied
 * ist) - dann geht es zu den Vereinen. False bei einem Fehler: Der Aufrufer
 * macht normal weiter, eine gescheiterte Anfrage hält eine Anmeldung nicht
 * auf, das Konto steht ja.
 *
 * Eine Stelle für alle drei Wege (angemeldet, nach der Anmeldung, nach der
 * Registrierung), damit die Meldungen nicht auseinanderlaufen.
 */
export async function beitrittMitMeldung(code: string, t: ReturnType<typeof useT>): Promise<boolean> {
  try {
    const mitgliedschaft = await api.post<ClubMembership>(`/api/clubs/invite/${code}/join`);
    // 1 = Approved: Wer schon drin ist, bekommt keine "Anfrage gesendet".
    // Eine schon länger offene Anfrage gibt der Server unverändert zurück
    // (idempotent) - dann ist "gesendet" falsch, sie wartet bereits.
    const verein = mitgliedschaft.clubName;
    const schonOffen = mitgliedschaft.status === 0 && Date.now() - Date.parse(mitgliedschaft.requestedAt) > 60_000;
    toast.success(
      mitgliedschaft.status === 1
        ? t("Du bist bereits Mitglied bei {verein}.", { verein })
        : schonOffen
          ? t("Deine Anfrage an {verein} wartet schon auf Freigabe.", { verein })
          : t("Beitrittsanfrage an {verein} gesendet.", { verein }),
    );
    return true;
  } catch (err) {
    toast.error(err instanceof ApiError ? err.message : t("Beitritt konnte nicht angefragt werden."));
    return false;
  }
}
