import { redirect } from "next/navigation";

/**
 * Den Aushang gibt es nur je Verein (/trainer/aushang/{clubId}). Die
 * Zurück-Schaltfläche der Unterseiten führt aber zur übergeordneten Adresse,
 * und die soll nicht ins Leere laufen.
 */
export default function AushangIndexPage() {
  redirect("/trainer/verein");
}
