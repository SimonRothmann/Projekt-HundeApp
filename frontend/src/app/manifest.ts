import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Dogity",
    short_name: "Dogity",
    description: "Trainingstagebuch, Zielplanung und Fährtenaufzeichnung für den Hundesport.",
    start_url: "/dashboard",
    display: "standalone",
    // Markenfarbe Indigo (primary) und heller Hintergrund, siehe DESIGN.md.
    background_color: "#f7f8fa",
    theme_color: "#5146ee",
    icons: [
      { src: "/icon.svg", sizes: "192x192", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "maskable" },
    ],
  };
}
