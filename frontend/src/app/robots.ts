import type { MetadataRoute } from "next";
import { SITE, absoluteUrl } from "@/lib/seo";

/**
 * Der eingeloggte Bereich wird ausgesperrt: Diese Seiten rendern ohne Login nur
 * eine leere Hülle. Nähme Google sie auf, stünden lauter inhaltsleere Treffer
 * im Index - das schadet der Bewertung der gesamten Domain, statt zu nützen.
 *
 * Bei den Anmeldeseiten ist es dieselbe Überlegung: ein Anmeldeformular
 * beantwortet keine Suchanfrage.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/dashboard",
        "/dogs",
        "/clubs",
        "/trainer",
        "/admin",
        "/profile",
        "/stats",
        "/sports",
        "/login",
        "/register",
        "/forgot-password",
        "/reset-password",
        "/offline",
        "/csp-report",
      ],
    },
    sitemap: absoluteUrl("/sitemap.xml"),
    host: SITE.url,
  };
}
