import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "CanisTrack",
    short_name: "CanisTrack",
    description: "Trainingstagebuch, Zielplanung und Fährtenaufzeichnung für den Hundesport.",
    start_url: "/dashboard",
    display: "standalone",
    background_color: "#f5f1e6",
    theme_color: "#2f4f3e",
    icons: [
      { src: "/icon.svg", sizes: "192x192", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "any" },
      { src: "/icon.svg", sizes: "512x512", type: "image/svg+xml", purpose: "maskable" },
    ],
  };
}
