import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import ts from "typescript";
import { describe, expect, it } from "vitest";

/**
 * Der Wächter gegen neue Lücken in der Übersetzung.
 *
 * i18n.test.ts prüft, dass jeder Satz in t("...") eine englische Fassung hat.
 * Was er nicht sieht, ist Text, der GAR NICHT durch t() läuft: ein Wort
 * direkt im JSX oder ein aria-label="Schließen". Genau diese Sorte Lücke
 * hat der Suchlauf vor Paket E1 an etwa dreißig Stellen gefunden - und sie
 * fällt erst auf, wenn jemand die App auf Englisch öffnet.
 *
 * Gelesen wird mit dem TypeScript-Compiler (keine Mustersuche auf Text), damit
 * ein Satz im Kommentar oder in einem Ausdruck nicht als Fund zählt und ein
 * mehrzeiliges JSX trotzdem erkannt wird.
 *
 * Geprüft werden JSX-Textknoten mit Buchstaben, Zeichenketten in den
 * Attributen placeholder, title, aria-label und alt, Zeichenketten in
 * Ausdrücken zwischen Elementen ({cond ? "Text" : "Text"}) und Meldungen, die
 * direkt an toast.*() oder set…Fehler/Meldung() gehen. Variablen und
 * Funktionsaufrufe außer t()/uebersetzbar() sieht der Wächter nicht - er ist
 * ein Netz für die häufigste Lücke, kein Beweis.
 */

const QUELLE = join(process.cwd(), "src");
const WURZELN = ["app", "components"];

/**
 * Was bewusst deutsch bleibt. Jede Zeile braucht ihren Grund; eine neue
 * gehört hierher nur, wenn der Text wirklich nicht zur Oberfläche zählt.
 */
const BEWUSST_DEUTSCH: { pfad: string; grund: string }[] = [
  { pfad: "app/opengraph-image.tsx", grund: "Vorschaubild für Suchmaschinen und Link-Vorschauen: die SEO-Fassung bleibt deutsch" },
  { pfad: "app/datenschutz/", grund: "Rechtstext: deutsche Fassung verbindlich; die englische Übersetzung ist ein zusammenhängender Text (datenschutz-en.tsx), kein Satzbaukasten für en.ts. Nur die Bedienelemente (fassungswahl.tsx) laufen über t()" },
  { pfad: "app/impressum/", grund: "Rechtstext nach deutschem Recht (TMG/DDG), Inhalt bleibt deutsch" },
  { pfad: "app/pruefungsordnungen/", grund: "Inhalt der deutschen Prüfungsordnungen (Inhaltssprache, nicht Oberflächensprache)" },
  { pfad: "app/sachkunde/", grund: "öffentliche Sachkunde-Inhalte: Fragenkatalog des SWHV, deutsch" },
  { pfad: "app/(app)/admin/", grund: "Adminbereich: nutzt nur der deutschsprachige Betreiber" },
  { pfad: "components/admin/", grund: "Adminbereich: nutzt nur der deutschsprachige Betreiber" },
];

/** Zeichenketten, die kein zu übersetzender Satz sind. */
const ERLAUBTE_LITERALE = new Set([
  "Dogity", // Markenname
  "OK", // in beiden Sprachen gleich
  "m", "s", // Einheiten (Meter, Sekunden)
  "GRUND", // Beispielcode im Sportarten-Editor, kein Satz
  "Website", // Köderfeld gegen Roboter im Anmeldeformular: aria-hidden, nie sichtbar
]);

/** Aufrufe, deren Zeichenkette die Person zu sehen bekommt. */
const ANZEIGE_AUFRUF = /^(?:toast(?:\.\w+)?|set(?:Error|Fehler|Meldung|Message|Hinweis|Info)\w*)$/;

const PRUEF_ATTRIBUTE = new Set(["placeholder", "title", "aria-label", "alt"]);

function alleDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return alleDateien(pfad);
    return /\.tsx$/.test(eintrag) ? [pfad] : [];
  });
}

/** Ein Text zählt als Satz, wenn er Buchstaben enthält und keine Adresse ist. */
function istZuUebersetzen(text: string): boolean {
  // Ø steht für "Durchschnitt" und gilt in beiden Sprachen.
  const roh = text.replace(/&\w+;|&#\d+;/g, " ").replace(/Ø/g, "").trim();
  if (!/\p{L}/u.test(roh)) return false;
  if (ERLAUBTE_LITERALE.has(roh)) return false;
  // Beispieladressen und Links ("name@example.com", "https://...") sind keine Sätze.
  if (/^\S+@\S+$/.test(roh) || /^https?:\/\/\S+$/.test(roh)) return false;
  return true;
}

type Fund = { zeile: number; text: string };

/**
 * Sammelt die Zeichenketten eines Attributwerts, die nicht durch
 * t()/uebersetzbar() laufen. Vergleiche (x === "a") sind Logik und keine
 * Anzeige; Aufrufe anderer Funktionen sieht der Wächter nicht.
 */
function sammleLiterale(knoten: ts.Node, quelle: ts.SourceFile, funde: Fund[]) {
  // Aufrufe sind nicht einsehbar: t()/uebersetzbar() sind erlaubt, bei allen
  // anderen entscheidet die aufgerufene Funktion.
  if (ts.isCallExpression(knoten)) return;
  // Verschachteltes JSX und Funktionen besucht der Rundgang in findeLuecken
  // selbst - hier würde sonst jedes className="flex" als Satz gelten.
  if (ts.isJsxElement(knoten) || ts.isJsxSelfClosingElement(knoten) || ts.isJsxFragment(knoten)) return;
  if (ts.isArrowFunction(knoten) || ts.isFunctionExpression(knoten)) return;
  // Bei && und || zählt nur die rechte Seite: links steht die Bedingung.
  if (ts.isBinaryExpression(knoten) && knoten.operatorToken.kind === ts.SyntaxKind.AmpersandAmpersandToken) {
    sammleLiterale(knoten.right, quelle, funde);
    return;
  }
  if (ts.isConditionalExpression(knoten)) {
    sammleLiterale(knoten.whenTrue, quelle, funde);
    sammleLiterale(knoten.whenFalse, quelle, funde);
    return;
  }
  if (ts.isBinaryExpression(knoten)) {
    const vergleich = [
      ts.SyntaxKind.EqualsEqualsEqualsToken,
      ts.SyntaxKind.ExclamationEqualsEqualsToken,
      ts.SyntaxKind.EqualsEqualsToken,
      ts.SyntaxKind.ExclamationEqualsToken,
    ].includes(knoten.operatorToken.kind);
    if (vergleich) return;
  }
  if (ts.isStringLiteral(knoten) || ts.isNoSubstitutionTemplateLiteral(knoten)) {
    if (istZuUebersetzen(knoten.text)) {
      funde.push({ zeile: quelle.getLineAndCharacterOfPosition(knoten.getStart()).line + 1, text: knoten.text });
    }
    return;
  }
  if (ts.isTemplateHead(knoten) || ts.isTemplateMiddle(knoten) || ts.isTemplateTail(knoten)) {
    if (istZuUebersetzen(knoten.text)) {
      funde.push({ zeile: quelle.getLineAndCharacterOfPosition(knoten.getStart()).line + 1, text: knoten.text });
    }
    return;
  }
  ts.forEachChild(knoten, (kind) => sammleLiterale(kind, quelle, funde));
}

/** Findet die nicht übersetzten Texte in einer .tsx-Quelle. */
function findeLuecken(inhalt: string, dateiname = "datei.tsx"): Fund[] {
  const quelle = ts.createSourceFile(dateiname, inhalt, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const funde: Fund[] = [];

  const besuche = (knoten: ts.Node) => {
    if (ts.isJsxText(knoten)) {
      const text = knoten.text;
      if (istZuUebersetzen(text)) {
        funde.push({
          zeile: quelle.getLineAndCharacterOfPosition(knoten.getStart()).line + 1,
          text: text.replace(/\s+/g, " ").trim(),
        });
      }
    } else if (ts.isJsxExpression(knoten) && knoten.expression && !ts.isJsxAttribute(knoten.parent)) {
      // {cond ? "Text" : "Text"} als Kind eines Elements.
      sammleLiterale(knoten.expression, quelle, funde);
    } else if (ts.isJsxAttribute(knoten) && PRUEF_ATTRIBUTE.has(knoten.name.getText(quelle)) && knoten.initializer) {
      sammleLiterale(knoten.initializer, quelle, funde);
    } else if (ts.isCallExpression(knoten) && ANZEIGE_AUFRUF.test(knoten.expression.getText(quelle))) {
      // Nur das erste Argument ist die Meldung; danach kommen Optionen ({ id, duration }).
      if (knoten.arguments[0]) sammleLiterale(knoten.arguments[0], quelle, funde);
    }
    ts.forEachChild(knoten, besuche);
  };
  besuche(quelle);
  return funde;
}

function istAusgenommen(relativerPfad: string): boolean {
  const pfad = relativerPfad.split("\\").join("/");
  return BEWUSST_DEUTSCH.some((a) => pfad.startsWith(a.pfad));
}

describe("Wächter gegen nicht übersetzten Text", () => {
  it("erkennt Text im JSX und in den Attributen", () => {
    const funde = findeLuecken(`
      export function A() {
        return <div title="Hinweis" aria-label={"Schließen"}>Hallo Welt <b>{x}</b></div>;
      }`);
    expect(funde.map((f) => f.text)).toStrictEqual(["Hinweis", "Schließen", "Hallo Welt"]);
  });

  it("lässt t(), Zeichen, Zahlen, Markenname und Beispieladressen durch", () => {
    const funde = findeLuecken(`
      export function A() {
        const t = useT();
        return (
          <div title={t("Hinweis")} aria-label={uebersetzbar("Schließen")}>
            · {n} / 12 → <span>Dogity</span> &nbsp;
            <input placeholder="name@example.com" alt="" />
            <a title={\`\${t("Anzahl")} · \${n}\`}>×</a>
            <b title={x === "a" ? t("A") : t("B")} />
          </div>
        );
      }`);
    expect(funde).toStrictEqual([]);
  });

  it("meldet Text in einer Bedingung und in einer Vorlage", () => {
    const funde = findeLuecken(`
      export const A = () => <i title={ok ? "Ja" : t("Nein")} placeholder={\`Wert \${n}\`} />;`);
    expect(funde.map((f) => f.text)).toStrictEqual(["Ja", "Wert "]);
  });

  it("findet in der ganzen Oberfläche keinen Text ohne t()", () => {
    const meldungen: string[] = [];
    for (const wurzel of WURZELN) {
      for (const datei of alleDateien(join(QUELLE, wurzel))) {
        const name = relative(QUELLE, datei);
        if (istAusgenommen(name)) continue;
        for (const fund of findeLuecken(readFileSync(datei, "utf8"), datei)) {
          meldungen.push(`${name}:${fund.zeile}  ${fund.text}`);
        }
      }
    }
    expect(meldungen, `Text ohne t():\n${meldungen.join("\n")}`).toStrictEqual([]);
  });

  it("führt nur Ausnahmen, die es noch gibt", () => {
    for (const a of BEWUSST_DEUTSCH) {
      expect(() => statSync(join(QUELLE, a.pfad)), a.pfad).not.toThrow();
    }
  });
});
