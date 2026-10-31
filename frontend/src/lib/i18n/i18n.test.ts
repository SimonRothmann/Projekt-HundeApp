import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { EN } from "./en";
import { mitBausteinen, uebersetze, uebersetzeMeldung } from "./index";

/**
 * Der Wächter über die Vollständigkeit.
 *
 * Weil der Schlüssel der deutsche Satz selbst ist, kann keine Typprüfung
 * merken, dass eine Übersetzung fehlt - `t("Neuer Satz")` ist immer gültiger
 * Code und liefert im Zweifel eben Deutsch. Genau das macht den Rückfall
 * gutmütig und das Vergessen leicht.
 *
 * Deshalb liest dieser Test den Quelltext und vergleicht jeden Aufruf mit der
 * englischen Liste. Er ist damit die einzige Stelle, an der "die App ist
 * vollständig übersetzt" überhaupt eine überprüfbare Aussage ist.
 */

const QUELLE = join(process.cwd(), "src");
/** Der Server liegt neben dem Frontend (frontend/ und backend/ im selben Repo). */
const BACKEND_QUELLE = join(process.cwd(), "..", "backend", "src");

function alleDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return alleDateien(pfad);
    return /\.tsx?$/.test(eintrag) && !/\.test\.tsx?$/.test(eintrag) ? [pfad] : [];
  });
}

/**
 * Entfernt Kommentare, damit ein Beispiel in einer Erklärung nicht als
 * echter Aufruf zählt - genau das ist beim Schreiben dieses Tests passiert.
 */
function ohneKommentare(inhalt: string): string {
  return inhalt.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
}


/**
 * Fehlermeldungen des Servers.
 *
 * Rund 300 deutsche Sätze kommen als { errors: ["..."] } vom Server und landen
 * über ApiError.message in den Toasts. Sie werden im Browser übersetzt (der
 * deutsche Satz ist der Schlüssel, siehe lib/api.ts) - also braucht jeder eine
 * englische Fassung, und dieser Test findet sie im C#-Quelltext.
 *
 * Gelesen wird mit schlichten Mustern statt mit einem C#-Parser. Das reicht,
 * weil die Meldungen in wenigen festen Formen stehen:
 *
 *  A  Argumente von Failure( und NotFound( - auch Result<T>.Failure(
 *  B  errors = new[] { "..." } in Controllern und Fehlerantworten.cs
 *  C  die Beschreibungen in DeutscherIdentityErrorDescriber
 *  D  Sätze in return-, error =-, meldungen =-, =>- und const string-Anweisungen
 *     (Prüfmethoden, die den Fehlertext zurückgeben), dazu Fail("...")
 *  E  das "was" in Textlaengen.ZuLang(.., .., "Die Notiz")
 *
 * Kommentare und Log-Aufrufe zählen nicht. Mit eingesetzten Werten ($"...")
 * sind nur einfache Platzhalter erlaubt ({hoechstens}); alles andere ist ein
 * Fehler dieses Tests, damit der Schlüssel lesbar bleibt - dann im Backend
 * den Wert vorher in eine Variable legen.
 */

/** Admin-Bereich und Importe: Der Adminbereich bleibt deutsch (siehe Paket E1). */
const NUR_ADMIN = [
  "Dogity.Api/Controllers/AdminController.cs",
  "Dogity.Api/Controllers/SachkundeAdminController.cs",
  "Dogity.Application/Admin/",
  "Dogity.Application/Learning/SachkundeAdminService.cs",
  "Dogity.Application/Notifications/MailTestService.cs",
  "Dogity.Application/Notifications/PushTestService.cs",
  "Dogity.Infrastructure/Import/",
];
/** Kein Fehlertext: Katalogdaten, Mailtexte, Datenexport. */
const KEINE_MELDUNGEN = ["Seed", "GroupTrainingStarterCatalog.cs", "MailVorlagen.cs", "AccountDataService.cs"];

/** Entfernt Kommentare, lässt Zeichenketten unberührt (ein "//" in einer URL ist kein Kommentar). */
function csOhneKommentare(code: string): string {
  let aus = "";
  for (let i = 0; i < code.length; ) {
    const c = code[i];
    if (c === '"') {
      const verbatim = code[i - 1] === "@" || code.slice(i - 2, i) === "$@";
      let j = i + 1;
      while (j < code.length) {
        if (verbatim ? code[j] === '"' && code[j + 1] === '"' : code[j] === "\\") { j += 2; continue; }
        if (code[j] === '"') break;
        j++;
      }
      aus += code.slice(i, j + 1);
      i = j + 1;
    } else if (c === "/" && code[i + 1] === "/") {
      while (i < code.length && code[i] !== "\n") i++;
    } else if (c === "/" && code[i + 1] === "*") {
      const ende = code.indexOf("*/", i + 2);
      i = ende < 0 ? code.length : ende + 2;
    } else {
      aus += c;
      i++;
    }
  }
  return aus;
}

type CsLiteral = { text: string; interpoliert: boolean };

/** Alle normalen und interpolierten Zeichenketten ($"..."); verbatim (@"...") sind Muster, keine Sätze. */
function csLiterale(span: string): CsLiteral[] {
  const gefunden: CsLiteral[] = [];
  for (const treffer of span.matchAll(/(\$@|@\$|\$|@)?"((?:[^"\\\n]|\\.)*)"/g)) {
    const vorsatz = treffer[1] ?? "";
    if (vorsatz.includes("@")) continue;
    gefunden.push({ text: treffer[2].replace(/\\"/g, '"').replace(/\\\\/g, "\\"), interpoliert: vorsatz === "$" });
  }
  return gefunden;
}

/** Position der schließenden Klammer zur öffnenden bei `start`; Zeichenketten werden übersprungen. */
function csKlammerEnde(code: string, start: number): number {
  const auf = code[start];
  const zu = auf === "(" ? ")" : auf === "{" ? "}" : "]";
  let tiefe = 0;
  for (let i = start; i < code.length; i++) {
    const c = code[i];
    if (c === '"') {
      i++;
      while (i < code.length && code[i] !== '"') i += code[i] === "\\" ? 2 : 1;
    } else if (c === auf) tiefe++;
    else if (c === zu && --tiefe === 0) return i;
  }
  return code.length;
}

// Anführungszeichen im Satz sind erlaubt (Ungültiger Wert für "{f}".) - die Zeichenketten sind einzeln gelesen.
const IST_SATZ = /^(?:[A-ZÄÖÜ]|\{\w+\})[^\n]* [^\n]*[.!?]$/;

/** Die Sätze einer C#-Quelle in den Formen A bis E (siehe oben). */
function csMeldungen(datei: string, quelltext: string): CsLiteral[] {
  const code = csOhneKommentare(quelltext);
  const funde: CsLiteral[] = [];

  // A: Failure( und NotFound( - jede Zeichenkette in den Argumenten, auch in Bedingungen.
  for (const m of code.matchAll(/\b(?:Failure|NotFound)\(/g)) {
    const auf = m.index + m[0].length - 1;
    funde.push(...csLiterale(code.slice(auf + 1, csKlammerEnde(code, auf))));
  }

  // B: errors = new[] { ... } bzw. errors = ["..."]
  for (const m of code.matchAll(/\berrors\s*=\s*(?=new\s*(?:string\s*)?\[\]|\[)/g)) {
    const ab = m.index + m[0].length;
    const klammer = code.slice(ab).startsWith("[") ? ab : code.indexOf("{", ab);
    funde.push(...csLiterale(code.slice(ab, csKlammerEnde(code, klammer) + 1)));
  }

  // C: Describer
  if (datei.endsWith("DeutscherIdentityErrorDescriber.cs")) {
    for (const m of code.matchAll(/Fehler\(nameof\(\w+\),\s*([^;]*);/g)) funde.push(...csLiterale(m[1]).slice(0, 1));
  }

  // D: Sätze in Anweisungen, die einen Fehlertext zurückgeben oder setzen
  for (const m of code.matchAll(/(?:\breturn\b|\b(?:error|meldungen)\s*=(?!=)|=>|\bconst\s+string\s+\w+\s*=|\.Fail\()/g)) {
    const ende = code.indexOf(";", m.index);
    funde.push(...csLiterale(code.slice(m.index + m[0].length, ende < 0 ? undefined : ende)).filter((l) => IST_SATZ.test(l.text) || m[0].endsWith("Fail(")));
  }

  // E: das "was" der Längenprüfung ("Die Notiz ist zu lang ...")
  for (const m of code.matchAll(/\bZuLang\(/g)) {
    const auf = m.index + m[0].length - 1;
    const letzte = csLiterale(code.slice(auf + 1, csKlammerEnde(code, auf))).at(-1);
    if (letzte && letzte.text !== "") funde.push(letzte);
  }
  return funde;
}

function csDateien(verzeichnis: string): string[] {
  return readdirSync(verzeichnis).flatMap((eintrag) => {
    if (eintrag === "obj" || eintrag === "bin") return [];
    const pfad = join(verzeichnis, eintrag);
    if (statSync(pfad).isDirectory()) return csDateien(pfad);
    return eintrag.endsWith(".cs") ? [pfad] : [];
  });
}

/** Schlüssel (Platzhalter bleiben als {name} stehen) und die Fehler, an denen sich der Test stößt. */
function sammleServermeldungen(): { meldungen: Map<string, string[]>; unlesbar: string[]; nurAdmin: Set<string> } {
  const meldungen = new Map<string, string[]>();
  const unlesbar: string[] = [];
  // Sätze aus dem Adminbereich: Sie müssen nicht übersetzt sein (der Adminbereich bleibt deutsch),
  // aber eine englische Fassung, die es dazu gibt, ist nicht "verwaist".
  const nurAdmin = new Set<string>();
  for (const pfad of csDateien(BACKEND_QUELLE)) {
    const name = pfad.slice(BACKEND_QUELLE.length + 1).split("\\").join("/");
    if (KEINE_MELDUNGEN.some((k) => name.includes(k))) continue;
    if (NUR_ADMIN.some((a) => name.startsWith(a) || name === a)) {
      for (const lit of csMeldungen(name, readFileSync(pfad, "utf8"))) nurAdmin.add(lit.text);
      continue;
    }
    for (const lit of csMeldungen(name, readFileSync(pfad, "utf8"))) {
      if (!/\p{L}/u.test(lit.text.replace(/\{[^}]*\}/gu, ""))) continue;
      if (lit.interpoliert && /\{(?!\w+\})/.test(lit.text)) {
        unlesbar.push(`${name}: ${lit.text}`);
        continue;
      }
      meldungen.set(lit.text, [...(meldungen.get(lit.text) ?? []), name]);
    }
  }
  return { meldungen, unlesbar, nurAdmin };
}

/**
 * Findet t("...") und uebersetzbar("..."), auch mehrzeilig geschrieben.
 *
 * Der Marker muss mit: Wo ein Satz als Datenfeld steht und später über
 * t(variable) gerendert wird, ist er im Quelltext sonst unsichtbar.
 */
function sammleSchluessel(): Map<string, string[]> {
  const gefunden = new Map<string, string[]>();
  const muster = /\b(?:t|uebersetzbar|uebersetzeAktuell)\(\s*"((?:[^"\\]|\\.)*)"/g;

  for (const datei of alleDateien(QUELLE)) {
    const inhalt = ohneKommentare(readFileSync(datei, "utf8"));
    for (const treffer of inhalt.matchAll(muster)) {
      const schluessel = treffer[1].replace(/\\"/g, '"').replace(/\\n/g, "\n");
      gefunden.set(schluessel, [...(gefunden.get(schluessel) ?? []), datei]);
    }
  }
  return gefunden;
}

describe("Englische Übersetzung", () => {
  const schluessel = sammleSchluessel();
  const server = sammleServermeldungen();

  it("findet überhaupt Aufrufe - sonst prüfte dieser Test nichts", () => {
    expect(schluessel.size).toBeGreaterThan(50);
  });

  it("übersetzt jeden verwendeten Satz", () => {
    const fehlend = [...schluessel.keys()].filter((s) => !(s in EN)).sort();
    expect(fehlend, `Ohne englische Fassung:\n${fehlend.join("\n")}`).toStrictEqual([]);
  });

  it("findet die Fehlermeldungen des Servers - sonst prüfte der nächste Test nichts", () => {
    // Zum Zeitpunkt der Aufnahme rund 200; fällt die Zahl stark, hat sich
    // das Muster im Backend geändert und das Einsammeln läuft ins Leere.
    expect(server.meldungen.size).toBeGreaterThan(150);
  });

  it("liest alle Servermeldungen mit eingesetzten Werten lesbar", () => {
    expect(server.unlesbar, `Platzhalter müssen einfache Namen sein:\n${server.unlesbar.join("\n")}`).toStrictEqual([]);
  });

  it("übersetzt jede Fehlermeldung des Servers", () => {
    const fehlend = [...server.meldungen.keys()].filter((s) => !(s in EN)).sort();
    expect(fehlend, `Servermeldung ohne englische Fassung:\n${fehlend.join("\n")}`).toStrictEqual([]);
  });

  it("führt keine Sätze, die es im Code nicht mehr gibt", () => {
    // Verwaiste Einträge sind kein Fehler, der etwas kaputt macht - aber sie
    // sammeln sich an und lassen die Liste größer aussehen als die Arbeit ist.
    const verwaist = Object.keys(EN).filter((s) => !schluessel.has(s) && !server.meldungen.has(s) && !server.nurAdmin.has(s)).sort();
    expect(verwaist, `Nicht mehr verwendet:\n${verwaist.join("\n")}`).toStrictEqual([]);
  });

  it("lässt keine englische Fassung leer", () => {
    const leer = Object.entries(EN).filter(([, wert]) => wert.trim() === "").map(([s]) => s);
    expect(leer).toStrictEqual([]);
  });

  it("behält die Platzhalter der deutschen Fassung bei", () => {
    // Ein verlorenes {name} fällt sonst erst auf, wenn jemand "Hallo ,"
    // auf dem Bildschirm sieht.
    const platzhalter = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();
    const abweichend = Object.entries(EN)
      .filter(([de, en]) => platzhalter(de).join() !== platzhalter(en).join())
      .map(([de]) => de);
    expect(abweichend).toStrictEqual([]);
  });
});

describe("uebersetze", () => {
  it("gibt auf Deutsch den Schlüssel selbst zurück", () => {
    expect(uebersetze("de", "Meine Hunde")).toBe("Meine Hunde");
  });

  it("fällt bei fehlender Übersetzung auf Deutsch zurück statt auf nichts", () => {
    expect(uebersetze("en", "Diesen Satz gibt es nicht")).toBe("Diesen Satz gibt es nicht");
  });

  it("setzt Platzhalter in beiden Sprachen ein", () => {
    expect(uebersetze("de", "{n} Hunde", { n: 3 })).toBe("3 Hunde");
  });

  it("lässt unbekannte Platzhalter stehen, statt sie zu leeren", () => {
    expect(uebersetze("de", "Hallo {name}", { andere: "x" })).toBe("Hallo {name}");
  });
});

describe("uebersetzeMeldung", () => {
  it("lässt Deutsch unberührt", () => {
    expect(uebersetzeMeldung("de", "Hund nicht gefunden.")).toBe("Hund nicht gefunden.");
  });

  it("übersetzt einen ganzen Satz", () => {
    expect(uebersetzeMeldung("en", "Hund nicht gefunden.")).toBe("Dog not found.");
  });

  it("zieht eingesetzte Werte aus dem fertigen Satz", () => {
    expect(uebersetzeMeldung("en", "Das Passwort muss mindestens 8 Zeichen lang sein.")).toBe(
      "The password must be at least 8 characters long.",
    );
  });

  it("übersetzt auch den eingesetzten Wert, wenn es ihn im Wörterbuch gibt", () => {
    // "Die Notiz" ist selbst ein Eintrag - der Satz ist Vorlage plus Wort.
    expect(uebersetzeMeldung("en", "Die Notiz ist zu lang (höchstens 500 Zeichen).")).toBe(
      "The note is too long (at most 500 characters).",
    );
  });

  it("übersetzt eine Zeilenmeldung des Anmeldungs-Imports samt Platzhalter", () => {
    // Diese Meldungen kommen als Datenfeld statt über ApiError und laufen an der Anzeigestelle durch uebersetzeMeldung.
    expect(uebersetzeMeldung("en", "Mehr als 40 Anmeldungen je Gruppe sind nicht möglich. Lösche alte Einträge.")).toBe(
      "More than 40 registrations per group are not possible. Delete old entries.",
    );
  });

  it("übersetzt eine Formfehler-Meldung mit Anführungszeichen", () => {
    expect(uebersetzeMeldung("en", 'Ungültiger Wert für "durationMinutes".')).toBe('Invalid value for "durationMinutes".');
  });

  it("lässt Unbekanntes deutsch stehen, statt zu raten", () => {
    expect(uebersetzeMeldung("en", "Diese Meldung kennt niemand.")).toBe("Diese Meldung kennt niemand.");
  });

  it("verwechselt kurze Vorlagen nicht mit beliebigen Meldungen", () => {
    // "{n} Hunde" hat zu wenig festen Text, um als Muster für Servermeldungen zu taugen.
    expect(uebersetzeMeldung("en", "5 Hunde")).toBe("5 Hunde");
  });

  it("kennt keine geerbten Objekt-Eigenschaften als Meldung", () => {
    expect(uebersetzeMeldung("en", "constructor")).toBe("constructor");
  });
});

describe("Servermeldungen im C#-Quelltext", () => {
  // Dieselbe Meldung kann in mehreren Formen auftauchen (return Failure(...)); gezählt wird einmal.
  const sätze = (datei: string, code: string) => [...new Set(csMeldungen(datei, code).map((l) => l.text))];

  it("liest Failure und NotFound samt Bedingungen", () => {
    const code = `
      return Result.Failure("Erster Satz.");
      return Result<Dto>.NotFound(
          a ? "Zweiter Satz." : "Dritter Satz.");`;
    expect(sätze("A.cs", code)).toStrictEqual(["Erster Satz.", "Zweiter Satz.", "Dritter Satz."]);
  });

  it("liest errors-Arrays, auch mehrzeilig", () => {
    const code = `return BadRequest(new { errors = new[] { "Vierter Satz." } });
      return Unauthorized(new { errors = new[]
      {
          "Fünfter Satz."
      } });`;
    expect(sätze("B.cs", code)).toStrictEqual(["Vierter Satz.", "Fünfter Satz."]);
  });

  it("liest Prüfmethoden, die den Text zurückgeben", () => {
    const code = `
      private const string Weg = "Sechster Satz hier.";
      string? Pruefe() { if (x) return "Siebter Satz hier."; error = "Achter Satz hier."; return null; }`;
    expect(sätze("D.cs", code)).toStrictEqual(["Sechster Satz hier.", "Siebter Satz hier.", "Achter Satz hier."]);
  });

  it("überspringt Kommentare, Logs und Zeichenketten, die keine Sätze sind", () => {
    const code = `
      // return Result.Failure("Im Kommentar.");
      /* Failure("Im Blockkommentar.") */
      logger.LogWarning("Ein Logeintrag mit Satzbau.");
      var url = "https://example.test/a//b";
      return Ok("kein Satz");
      var x = 1;`;
    expect(sätze("E.cs", code)).toStrictEqual([]);
  });

  it("liest Sätze mit Anführungszeichen und Bedingungsketten (Fehlerantworten.cs)", () => {
    const code = `var meldungen = leer
          ? ["Neunter Satz hier."]
          : felder.Select(f => $"Zehnter Wert für \\"{f}\\".").ToArray();`;
    expect(sätze("H.cs", code)).toStrictEqual(["Neunter Satz hier.", 'Zehnter Wert für "{f}".']);
  });

  it("findet die Formfehler-Meldungen in Fehlerantworten.cs", () => {
    const pfad = join(BACKEND_QUELLE, "Dogity.Api", "Hosting", "Fehlerantworten.cs");
    const gelesen = sätze("Fehlerantworten.cs", readFileSync(pfad, "utf8"));
    expect(gelesen).toContain("Die Anfrage ist unvollständig oder fehlerhaft.");
    expect(gelesen).toContain('Ungültiger Wert für "{f}".');
    expect(gelesen).toContain("Ein Text ist zu lang.");
  });

  it("liest das Wort der Längenprüfung", () => {
    expect(sätze("F.cs", `var z = Textlaengen.ZuLang(text, 500, "Die Notiz");`)).toStrictEqual(["Die Notiz"]);
  });

  it("merkt sich, welche Zeichenketten Werte einsetzen", () => {
    const [lit] = csMeldungen("G.cs", `return Result.Failure($"Höchstens {n} Stück.");`);
    expect(lit).toStrictEqual({ text: "Höchstens {n} Stück.", interpoliert: true });
  });
});

describe("mitBausteinen", () => {
  it("setzt den Baustein an die Stelle des Platzhalters", () => {
    const teile = mitBausteinen("Lies die {regeln} jetzt.", { regeln: "REGELN" }) as unknown[];
    expect(teile[0]).toBe("Lies die ");
    expect((teile[1] as { props: { children: string } }).props.children).toBe("REGELN");
    expect(teile[2]).toBe(" jetzt.");
  });

  it("lässt unbekannte Platzhalter als Text stehen", () => {
    const teile = mitBausteinen("Hallo {name}", { anderes: "x" }) as unknown[];
    expect(teile).toContain("{name}");
  });

  it("verträgt einen Satz, in dem der Baustein am Anfang oder Ende steht", () => {
    const teile = mitBausteinen("{a} und {b}", { a: "A", b: "B" }) as unknown[];
    expect(teile).toHaveLength(5);
  });
});
