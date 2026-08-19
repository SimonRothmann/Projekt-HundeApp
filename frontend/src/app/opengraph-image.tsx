import { ImageResponse } from "next/og";
import { SITE } from "@/lib/seo";

/**
 * Vorschaubild für geteilte Verweise (WhatsApp, Facebook, Mastodon). Ohne das
 * erscheint beim Teilen nur ein grauer Kasten - und im Hundesport läuft die
 * Weiterempfehlung fast vollständig über Gruppenchats.
 *
 * Bewusst gezeichnet statt als Bilddatei: so bleibt es mit dem Markenauftritt
 * in einer Datei und muss nicht bei jeder Textänderung neu exportiert werden.
 */
export const alt = "Dogity – Trainingstagebuch für den Hundesport";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "80px",
          background: "linear-gradient(135deg, #0b0f1a 0%, #1b1740 55%, #312a6b 100%)",
          color: "#f8fafc",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ fontSize: 40, fontWeight: 700, color: "#a5b4fc" }}>{SITE.name}</div>
        <div style={{ fontSize: 76, fontWeight: 800, lineHeight: 1.1, marginTop: 16 }}>
          Trainingstagebuch für den Hundesport
        </div>
        <div style={{ fontSize: 34, color: "#c7d2fe", marginTop: 28 }}>
          Training festhalten · Fährten per GPS auswerten · Prüfungen vorbereiten
        </div>
      </div>
    ),
    size,
  );
}
