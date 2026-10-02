/**
 * Welche der zwei Ansichten die Gruppenseite zeigt: die Mitglieder (die
 * Gruppe als Gemeinschaft mit Konto) oder die Anmeldungen (Kursteilnehmende
 * ohne Konto, z. B. Welpengruppe).
 *
 * Als reine Funktion, weil die Entscheidung drei Quellen hat - den Parameter
 * in der Adresse, die Zahlen und die Rechte - und genau dort Fehler stecken:
 * ein von Hand getippter Wert, ein alter Link aus einer Benachrichtigung an
 * jemanden, der die Anmeldungen gar nicht sehen darf.
 */
export type GruppenAnsicht = "mitglieder" | "anmeldungen";

/** Name des Parameters in der Adresse: /trainer/{id}?ansicht=anmeldungen. */
export const ANSICHT_PARAMETER = "ansicht";

/** Nur die zwei bekannten Werte; alles andere (leer, falsch geschrieben, fremd) ist "keine Angabe". */
export function ansichtAusParameter(wert: string | null | undefined): GruppenAnsicht | null {
  return wert === "mitglieder" || wert === "anmeldungen" ? wert : null;
}

export function waehleAnsicht(
  parameter: string | null | undefined,
  zahlen: {
    /** null = der Server liefert die Anmeldungen nicht (kein Recht) oder sie sind noch nicht da. */
    anmeldungen: number | null;
    mitglieder: number;
  },
): GruppenAnsicht {
  // Ohne Zugriff gibt es die Anmeldungen-Ansicht nicht - auch nicht über einen Link.
  if (zahlen.anmeldungen === null) return "mitglieder";
  const gewuenscht = ansichtAusParameter(parameter);
  if (gewuenscht) return gewuenscht;
  // Eine reine Anmeldegruppe (Welpenkurs) hat keine Mitglieder: Dort wäre die erste Ansicht leer.
  return zahlen.anmeldungen > 0 && zahlen.mitglieder === 0 ? "anmeldungen" : "mitglieder";
}
