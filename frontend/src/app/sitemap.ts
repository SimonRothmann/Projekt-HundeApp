import type { MetadataRoute } from "next";
import { absoluteUrl } from "@/lib/seo";
import { getCatalog, neuesteFassung } from "@/lib/public-catalog";
import { getQuizCatalogs } from "@/lib/public-sachkunde";
import { AKTUELLE_VERSION_DATUM } from "@/lib/versionshinweise";

/**
 * Verzeichnis aller öffentlichen Seiten. Ohne das müsste Google jede
 * Prüfungsordnungsseite über Verweise finden; mit Sitemap kennt es sie sofort.
 *
 * Die Prüfungsordnungen kommen aus dem Backend, nicht aus einer festen Liste -
 * eine neu angelegte Sportart steht damit von selbst drin.
 *
 * Zum Änderungsdatum: Es steht nur dort, wo wir ein echtes haben. Früher trug
 * jede der fünfzig Adressen "gerade eben" - bei einer Prüfungsordnung, die
 * sich seit Jahren nicht geändert hat, schlicht gelogen. Google gleicht
 * lastmod mit der Seite ab und verwirft das Feld für die GANZE Sitemap,
 * sobald es unglaubwürdig wird; wir hätten also nichts gewonnen und das
 * Signal für jede Seite mit echtem Datum gleich mit verschenkt. Wo der
 * Inhalt im Code steht (Startseite, Sachkunde), lassen wir es deshalb weg:
 * keine Angabe ist besser als eine erfundene.
 */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const catalog = await getCatalog();

  const staticPages: MetadataRoute.Sitemap = [
    // Inhalt steht im Code, kein Änderungsdatum verfügbar - siehe oben.
    { url: absoluteUrl("/"), changeFrequency: "monthly", priority: 1 },
    {
      url: absoluteUrl("/pruefungsordnungen"),
      lastModified: neuesteFassung(catalog),
      changeFrequency: "monthly",
      priority: 0.8,
    },
    { url: absoluteUrl("/sachkunde"), changeFrequency: "monthly", priority: 0.8 },
    // Ändert sich häufiger als jede andere Seite - das ist ihr Zweck. Und sie
    // kennt ihr Datum auf den Tag genau: den der neuesten Fassung.
    {
      url: absoluteUrl("/neuerungen"),
      lastModified: AKTUELLE_VERSION_DATUM,
      changeFrequency: "weekly",
      priority: 0.4,
    },
  ];

  const regulationPages: MetadataRoute.Sitemap = catalog.map((entry) => ({
    url: absoluteUrl(`/pruefungsordnungen/${entry.slug}`),
    // Gültig-ab der geführten Fassung: genau dann ändert sich diese Seite.
    lastModified: entry.regulation.currentVersionValidFrom ?? undefined,
    changeFrequency: "yearly",
    priority: 0.7,
  }));

  // Eigener Name statt "catalog": der Prüfungsordnungskatalog steht oben schon
  // unter diesem Bezeichner, und ein verdeckter Name ist hier ein stiller Fehler.
  const quizPages: MetadataRoute.Sitemap = (await getQuizCatalogs()).map((fragenkatalog) => ({
    url: absoluteUrl(`/sachkunde/${fragenkatalog.code.toLowerCase()}`),
    changeFrequency: "yearly",
    priority: 0.7,
  }));

  return [...staticPages, ...regulationPages, ...quizPages];
}
