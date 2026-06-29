import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Dogity",
    short_name: "Dogity",
    description: "Trainingstagebuch, Zielplanung und Fährtenaufzeichnung für den Hundesport.",
    start_url: "/dashboard",
    display: "standalone",
    // An die Apple-orientierte Palette in globals.css angelehnt (siehe
    // DESIGN_SYSTEM.md "Farben"): Systemblau als Markenfarbe, neutrales,
    // fast weißes Grau als Hintergrund statt der vorherigen Sand/Waldgrün-Töne.
    background_color: "#f7f8fa",
    theme_color: "#007aff",
    icons: [
      { src: "/icon.svg", sizes: "192x192", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "maskable" },
    ],
  };
}
