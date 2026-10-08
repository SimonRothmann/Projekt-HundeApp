import { getQuizCatalogs } from "@/lib/public-sachkunde";
import { KatalogListe } from "@/components/sachkunde/katalog-liste";
import { LernenUeberschrift } from "@/components/sachkunde/lernen-ueberschrift";

/**
 * Sachkunde üben innerhalb der App: dieselben Kataloge und derselbe Trainer
 * wie unter /sachkunde, aber mit unterer Leiste und App-Kopf. Angemeldete
 * werden von der öffentlichen Seite hierher geschickt (siehe
 * lib/sachkunde-weiterleitung.ts). Die Kataloge sind öffentliche Stammdaten
 * und werden deshalb wie dort auf dem Server geholt.
 */
export default async function LernenPage() {
  const catalogs = await getQuizCatalogs();

  return (
    <div className="flex flex-col gap-6">
      <LernenUeberschrift />
      <KatalogListe catalogs={catalogs} basis="/lernen" />
    </div>
  );
}
