import {
  AMPEL_FARBEN,
  faerbeSegmente,
  formatiereMassstab,
  massstabsbalken,
  passeEin,
  type Ampel,
  type BildTexte,
  type Einpassung,
  type FaehrtenGeometrie,
} from "@/lib/faehrten-bild";

/**
 * Zeichnet das Fährtenbild auf eine Canvas-Fläche (Hochformat, 1080 x 1350).
 *
 * Alle Texte kommen fertig übersetzt herein - hier wird nur gezeichnet. Das
 * Bild hat einen hellen Grund, unabhängig vom Erscheinungsbild der App: Es
 * landet in einem Messenger, nicht im Dark Mode dieser App. Es enthält weder
 * Koordinaten noch Uhrzeiten noch eine Karte (siehe faehrten-bild.ts).
 */

export const BILD_BREITE = 1080;
export const BILD_HOEHE = 1350;

const RAND = 72;
const GRUND = "#fbfaf7";
const TEXT = "#1c1917";
const GEDAEMPFT = "#57534e";
const KARTENGRUND = "#f0eee7";
const GELEGT_LINIE = "#b4b0a6";
const OHNE_ABWEICHUNG = "#64748b";
const MARKE_FARBE = "#4f46e5";

const KARTE = { x: RAND, y: 240, breite: BILD_BREITE - 2 * RAND, hoehe: 640 };
const KARTEN_RAND = 56;
/** Unten in der Karte bleibt Platz für Maßstabsbalken und Legende. */
const KARTEN_FUSS = 130;
const MAX_ZEILEN = 4;

export type BildEingabe = {
  geometrie: FaehrtenGeometrie;
  ampel: Ampel | null;
  texte: BildTexte;
  /** CSS-Schriftfamilie - die der App, wenn verfügbar. */
  schrift: string;
};

function rundesRechteck(ctx: CanvasRenderingContext2D, x: number, y: number, breite: number, hoehe: number, radius: number) {
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.arcTo(x + breite, y, x + breite, y + hoehe, radius);
  ctx.arcTo(x + breite, y + hoehe, x, y + hoehe, radius);
  ctx.arcTo(x, y + hoehe, x, y, radius);
  ctx.arcTo(x, y, x + breite, y, radius);
  ctx.closePath();
}

/** Verkleinert die Schrift, bis der Text in die Breite passt. */
function passendeSchrift(
  ctx: CanvasRenderingContext2D,
  text: string,
  gewicht: number,
  startPx: number,
  minPx: number,
  maxBreite: number,
  schrift: string,
): string {
  let px = startPx;
  ctx.font = `${gewicht} ${px}px ${schrift}`;
  while (px > minPx && ctx.measureText(text).width > maxBreite) {
    px -= 2;
    ctx.font = `${gewicht} ${px}px ${schrift}`;
  }
  return ctx.font;
}

/** Kürzt auf die Breite, falls auch die kleinste Schrift nicht reicht. */
function gekuerzt(ctx: CanvasRenderingContext2D, text: string, maxBreite: number): string {
  if (ctx.measureText(text).width <= maxBreite) return text;
  let kurz = text;
  while (kurz.length > 1 && ctx.measureText(`${kurz}…`).width > maxBreite) kurz = kurz.slice(0, -1);
  return `${kurz.trimEnd()}…`;
}

function linie(ctx: CanvasRenderingContext2D, einpassung: Einpassung, punkte: { x: number; y: number }[], von: number, bis: number) {
  ctx.beginPath();
  for (let i = von; i <= bis; i++) {
    const { x, y } = einpassung.abbilden(punkte[i]);
    if (i === von) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.stroke();
}

function zeichneKarte(ctx: CanvasRenderingContext2D, eingabe: BildEingabe) {
  const { geometrie, schrift } = eingabe;

  ctx.fillStyle = KARTENGRUND;
  rundesRechteck(ctx, KARTE.x, KARTE.y, KARTE.breite, KARTE.hoehe, 32);
  ctx.fill();

  // Eingepasst wird alles zusammen, damit der Ablauf nicht über den Rand läuft.
  const alle = [...geometrie.gelegt, ...geometrie.marker.map((m) => m.p), ...(geometrie.ablauf?.punkte ?? [])];
  const einpassung = passeEin(
    alle,
    { x: KARTE.x, y: KARTE.y, breite: KARTE.breite, hoehe: KARTE.hoehe - KARTEN_FUSS },
    KARTEN_RAND,
  );

  ctx.lineCap = "round";
  ctx.lineJoin = "round";

  // Die gelegte Fährte: breit und grau, darüber liegt der Ablauf.
  ctx.strokeStyle = GELEGT_LINIE;
  ctx.lineWidth = 22;
  linie(ctx, einpassung, geometrie.gelegt, 0, geometrie.gelegt.length - 1);

  if (geometrie.ablauf) {
    ctx.lineWidth = 9;
    const { punkte, abweichungen } = geometrie.ablauf;
    for (const stueck of faerbeSegmente(abweichungen)) {
      ctx.strokeStyle = stueck.stufe ? AMPEL_FARBEN[stueck.stufe] : OHNE_ABWEICHUNG;
      linie(ctx, einpassung, punkte, stueck.von, stueck.bis);
    }
  }

  // Gegenstände und andere Marker: kleine Quadrate mit hellem Rand, damit sie
  // auf jeder Linienfarbe stehen.
  for (const marker of geometrie.marker) {
    const { x, y } = einpassung.abbilden(marker.p);
    const seite = 26;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(x - seite / 2 - 4, y - seite / 2 - 4, seite + 8, seite + 8);
    ctx.fillStyle = marker.gegenstand ? TEXT : "#78716c";
    ctx.fillRect(x - seite / 2, y - seite / 2, seite, seite);
  }

  // Der Startpunkt ist der erste Punkt der gelegten Fährte.
  const start = einpassung.abbilden(geometrie.gelegt[0]);
  ctx.beginPath();
  ctx.arc(start.x, start.y, 19, 0, Math.PI * 2);
  ctx.fillStyle = "#ffffff";
  ctx.fill();
  ctx.beginPath();
  ctx.arc(start.x, start.y, 12, 0, Math.PI * 2);
  ctx.fillStyle = TEXT;
  ctx.fill();

  const kartenUnten = KARTE.y + KARTE.hoehe;

  // Maßstabsbalken unten links.
  const balken = massstabsbalken(einpassung.masstab, 300);
  if (balken) {
    const x = KARTE.x + 44;
    const y = kartenUnten - 92;
    ctx.strokeStyle = TEXT;
    ctx.lineWidth = 5;
    ctx.lineCap = "butt";
    ctx.beginPath();
    ctx.moveTo(x, y - 12);
    ctx.lineTo(x, y);
    ctx.lineTo(x + balken.breitePx, y);
    ctx.lineTo(x + balken.breitePx, y - 12);
    ctx.stroke();
    ctx.fillStyle = TEXT;
    ctx.font = `600 30px ${schrift}`;
    ctx.textAlign = "left";
    ctx.fillText(formatiereMassstab(balken.meter), x + balken.breitePx + 16, y + 10);
  }

  zeichneLegende(ctx, eingabe, kartenUnten - 34);
}

/** Legende in einer Zeile unten in der Karte; bricht um, wenn sie nicht passt. */
function zeichneLegende(ctx: CanvasRenderingContext2D, eingabe: BildEingabe, grundlinie: number) {
  const { geometrie, texte, schrift } = eingabe;
  type Eintrag = { text: string; symbol: "start" | "gegenstand" | "gelegt" | "abgelaufen" };
  const eintraege: Eintrag[] = [{ text: texte.legende.start, symbol: "start" }];
  if (geometrie.marker.some((m) => m.gegenstand)) eintraege.push({ text: texte.legende.gegenstand, symbol: "gegenstand" });
  eintraege.push({ text: texte.legende.gelegt, symbol: "gelegt" });
  if (geometrie.ablauf) eintraege.push({ text: texte.legende.abgelaufen, symbol: "abgelaufen" });

  ctx.font = `500 28px ${schrift}`;
  ctx.textAlign = "left";
  const symbolBreite = 44;
  const luecke = 34;
  const gesamt = eintraege.reduce((summe, e) => summe + symbolBreite + ctx.measureText(e.text).width, 0) + luecke * (eintraege.length - 1);
  const verfuegbar = KARTE.breite - 88;
  // Passt die Legende nicht in eine Zeile (englische Texte, schmale Schrift),
  // schrumpft sie, statt abgeschnitten zu werden.
  const faktor = Math.min(1, verfuegbar / gesamt);
  if (faktor < 1) ctx.font = `500 ${Math.max(18, Math.floor(28 * faktor))}px ${schrift}`;

  let x = KARTE.x + 44;
  for (const eintrag of eintraege) {
    const mitte = grundlinie - 10;
    ctx.save();
    ctx.lineCap = "round";
    if (eintrag.symbol === "start") {
      ctx.fillStyle = TEXT;
      ctx.beginPath();
      ctx.arc(x + 12, mitte, 10, 0, Math.PI * 2);
      ctx.fill();
    } else if (eintrag.symbol === "gegenstand") {
      ctx.fillStyle = TEXT;
      ctx.fillRect(x + 3, mitte - 10, 20, 20);
    } else if (eintrag.symbol === "gelegt") {
      ctx.strokeStyle = GELEGT_LINIE;
      ctx.lineWidth = 12;
      ctx.beginPath();
      ctx.moveTo(x + 4, mitte);
      ctx.lineTo(x + 28, mitte);
      ctx.stroke();
    } else {
      // Ampel in drei Stücken: die Farbe bedeutet die Abweichung.
      ctx.lineWidth = 8;
      (["gruen", "gelb", "rot"] as const).forEach((stufe, i) => {
        ctx.strokeStyle = AMPEL_FARBEN[stufe];
        ctx.beginPath();
        ctx.moveTo(x + 2 + i * 13, mitte);
        ctx.lineTo(x + 12 + i * 13, mitte);
        ctx.stroke();
      });
    }
    ctx.restore();
    ctx.fillStyle = GEDAEMPFT;
    ctx.fillText(eintrag.text, x + symbolBreite, grundlinie);
    x += symbolBreite + ctx.measureText(eintrag.text).width + luecke;
  }
}

export function zeichneFaehrtenbild(ctx: CanvasRenderingContext2D, eingabe: BildEingabe) {
  const { texte, schrift, ampel } = eingabe;
  const innenBreite = BILD_BREITE - 2 * RAND;
  const akzent = ampel ? AMPEL_FARBEN[ampel] : TEXT;

  ctx.fillStyle = GRUND;
  ctx.fillRect(0, 0, BILD_BREITE, BILD_HOEHE);
  ctx.textAlign = "left";
  ctx.textBaseline = "alphabetic";

  // Kopf: Hund und Fährte, darunter der Tag.
  ctx.fillStyle = TEXT;
  passendeSchrift(ctx, texte.titel, 800, 72, 36, innenBreite, schrift);
  ctx.fillText(gekuerzt(ctx, texte.titel, innenBreite), RAND, 150);
  if (texte.datum) {
    ctx.fillStyle = GEDAEMPFT;
    ctx.font = `500 40px ${schrift}`;
    ctx.fillText(texte.datum, RAND, 208);
  }

  zeichneKarte(ctx, eingabe);

  // Kennzahlen.
  ctx.fillStyle = akzent;
  passendeSchrift(ctx, texte.kopfzahl, 800, 96, 44, innenBreite, schrift);
  ctx.fillText(gekuerzt(ctx, texte.kopfzahl, innenBreite), RAND, 985);

  let y = 1060;
  if (texte.ampelWort) {
    ctx.fillStyle = akzent;
    ctx.font = `700 46px ${schrift}`;
    ctx.fillText(gekuerzt(ctx, texte.ampelWort, innenBreite), RAND, 1040);
    y = 1106;
  }
  ctx.fillStyle = TEXT;
  for (const zeile of texte.zeilen.slice(0, MAX_ZEILEN)) {
    ctx.font = `500 40px ${schrift}`;
    ctx.fillText(gekuerzt(ctx, zeile, innenBreite), RAND, y);
    y += 50;
  }

  // Fußzeile: der Schriftzug, sonst nichts.
  ctx.fillStyle = MARKE_FARBE;
  ctx.font = `800 40px ${schrift}`;
  if ("letterSpacing" in ctx) (ctx as CanvasRenderingContext2D & { letterSpacing: string }).letterSpacing = "2px";
  ctx.textAlign = "right";
  ctx.fillText(texte.marke, BILD_BREITE - RAND, 1298);
  ctx.textAlign = "left";
  if ("letterSpacing" in ctx) (ctx as CanvasRenderingContext2D & { letterSpacing: string }).letterSpacing = "0px";
}
