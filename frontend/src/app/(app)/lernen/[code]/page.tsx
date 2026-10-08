import { notFound } from "next/navigation";
import { getQuizCatalog, getQuizCatalogs, getQuizQuestions } from "@/lib/public-sachkunde";
import { QuizTrainer } from "@/components/sachkunde/quiz-trainer";
import { FragenUebersicht } from "@/components/sachkunde/fragen-uebersicht";
import { KatalogDaten } from "@/components/sachkunde/katalog-liste";

type Props = { params: Promise<{ code: string }> };

/** Wie die öffentliche Seite: beide Kataloge vorbauen. */
export async function generateStaticParams() {
  const catalogs = await getQuizCatalogs();
  return catalogs.map((catalog) => ({ code: catalog.code.toLowerCase() }));
}

/**
 * Ein Fragenkatalog innerhalb der App - dieselben Bausteine wie unter
 * /sachkunde/[code] (Trainer und Fragenübersicht), nur ohne den
 * Suchmaschinen-Rahmen. Den Rückweg übernimmt der Zurück-Knopf der App.
 */
export default async function LernenKatalogPage({ params }: Props) {
  const { code } = await params;
  const catalog = await getQuizCatalog(code);
  if (!catalog) notFound();

  const fragen = await getQuizQuestions(catalog.code);

  return (
    <div className="mx-auto w-full max-w-2xl min-w-0">
      <h1 className="text-2xl font-semibold tracking-tight text-balance">{catalog.name}</h1>
      <KatalogDaten catalog={catalog} />

      <div className="mt-6">
        <QuizTrainer catalog={catalog} />
      </div>

      <FragenUebersicht fragen={fragen} />
    </div>
  );
}
