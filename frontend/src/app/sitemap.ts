import type { MetadataRoute } from "next";
import { absoluteUrl } from "@/lib/seo";
import { getCatalog } from "@/lib/public-catalog";

/**
 * Verzeichnis aller öffentlichen Seiten. Ohne das müsste Google jede
 * Prüfungsordnungsseite über Verweise finden; mit Sitemap kennt es sie sofort.
 *
 * Die Prüfungsordnungen kommen aus dem Backend, nicht aus einer festen Liste -
 * eine neu angelegte Sportart steht damit von selbst drin.
 */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const now = new Date();

  const staticPages: MetadataRoute.Sitemap = [
    { url: absoluteUrl("/"), lastModified: now, changeFrequency: "monthly", priority: 1 },
    {
      url: absoluteUrl("/pruefungsordnungen"),
      lastModified: now,
      changeFrequency: "monthly",
      priority: 0.8,
    },
  ];

  const catalog = await getCatalog();
  const regulationPages: MetadataRoute.Sitemap = catalog.map((entry) => ({
    url: absoluteUrl(`/pruefungsordnungen/${entry.slug}`),
    lastModified: now,
    changeFrequency: "yearly",
    priority: 0.7,
  }));

  return [...staticPages, ...regulationPages];
}
