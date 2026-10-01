import { redirect } from "next/navigation";

/**
 * Den Aushang gibt es nur je Gruppe (/trainer/aushang/gruppe/{groupId}); die
 * Zurück-Schaltfläche der Unterseiten führt aber zur übergeordneten Adresse.
 */
export default function GruppenAushangIndexPage() {
  redirect("/trainer");
}
