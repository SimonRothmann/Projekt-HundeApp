export const meta = {
  name: 'dogity-paket-umsetzen',
  description: 'Ein Dogity-Paket umsetzen: Sonnet implementiert, unabhängige Prüfung, Nachbesserung (kein Commit)',
  phases: [
    { title: 'Umsetzung', detail: 'Sonnet setzt die Spezifikation um und prüft selbst', model: 'sonnet' },
    { title: 'Prüfung', detail: 'unabhängiger Review des Diffs', model: 'sonnet' },
    { title: 'Nachbesserung', detail: 'Befunde hoch/mittel beheben', model: 'sonnet' },
  ],
}

const REPO = '/Users/simon/Workspace/Privat/Projekt-HundeApp'

const REGELN = `Repo: ${REPO} (Branch master). Dogity = Hundesport-App: frontend/ (Next.js App Router, React, Tailwind, vitest), backend/ (.NET 10, EF Core 10, Postgres; Schichten Domain/Application/Infrastructure/Api; Tests in backend/tests).

Werkzeuge: vor jedem Befehl `export PATH=/opt/homebrew/bin:$PATH DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec DOTNET_CLI_TELEMETRY_OPTOUT=1` (node, npm, dotnet kommen aus Homebrew).

Verbindliche Regeln:
- Das Repo ist öffentlich: keine Angriffswege in Doku, TODO oder Kommentaren beschreiben, solange eine Lücke offen ist; keine Geheimnisse.
- Lies zuerst CLAUDE.md (vier Grundsätze der Arbeitsweise), AI_RULES.md und CODING_GUIDELINES.md (kurz). Schreib Code wie der umgebende Code: Benennung, Idiom, Kommentardichte. Kommentare erklären das Warum, auf Deutsch wie im Rest.
- Backend: Result-Muster und dünne Controller wie vorhanden. EF-Konfigurationen in Infrastructure/Persistence/Configurations. Soft Delete (DeletedAt) + globale Query-Filter in ApplicationDbContext - neue Entitäten brauchen beides. Migration mit dotnet ef (Befehl/Pfade aus vorhandenen Migrationen ableiten); Defaults so, dass bestehende Zeilen gültig bleiben. Enum-Speicherung wie beim Nachbar-Enum derselben Entität.
- Personenbezogene Daten: Neue Daten müssen in AccountDataService (Datenexport UND Kontolöschung/Purge) berücksichtigt sein; bei neuer Verarbeitung docs/VERARBEITUNGSVERZEICHNIS.md und frontend/src/app/datenschutz/page.tsx nachziehen (und das STAND-Datum in frontend/src/lib/rechtliches.ts auf den Tag der Umsetzung) - nur dann, sonst Rechtstexte nicht anfassen.
- Freitextfelder: Längengrenze in backend Textlaengen.cs UND frontend/src/lib/textlaengen.ts, maxLength am Eingabefeld, Prüfung im Service.
- Frontend: api-Helfer aus lib/api.ts, UI-Bausteine aus components/ui. JEDER sichtbare Text über useT()/t() bzw. uebersetzbar() mit dem deutschen Satz als Schlüssel UND englischer Übersetzung in frontend/src/lib/i18n/en.ts (i18n.test.ts erzwingt das; keine doppelten Schlüssel). Kein dangerouslySetInnerHTML/innerHTML (html-senken.test.ts wacht). localStorage/sessionStorage nur mit try/catch.
- Mobile-App-first: muss bei 375 px Breite ohne horizontalen Scroll funktionieren, Touch-Ziele ausreichend groß, lange Texte umbrechen (min-w-0, break-words).
- Versionshinweis: In frontend/src/lib/versionshinweise.ts oben die Fassung anlegen, die die Spezifikation nennt (datum = Tag der Umsetzung; fehlt sie dort, keine anlegen), Format und Ton wie der oberste vorhandene Eintrag, kein Technikjargon, Admin-Only-Änderungen nicht erwähnen. Texte über uebersetzbar() + en.ts.
- Keine neuen Abhängigkeiten, außer die Spezifikation erlaubt ausdrücklich eine.
- Tests: Backend-Tests (xunit, Muster der vorhandenen Tests in backend/tests/Dogity.Application.Tests) für neue Service-Logik inklusive Rechteprüfungen; vitest für reine Frontend-Logik (lib/*.test.ts). Bestehende Tests niemals abschwächen oder löschen.
- Pflicht vor dem Abschluss, alles grün: (cd backend && dotnet build && dotnet test) und (cd frontend && npx tsc --noEmit && npm run lint && npx vitest run && npx next build).
- Verboten: git commit/push, ssh, Änderungen an deploy/ oder am Server, dauerhaft laufende Server starten, Inhalte von .env oder Geheimnisse ausgeben.
- Budget: gezielt arbeiten (rg/grep, sed -n Bereiche), keine unnötigen Voll-Dumps großer Dateien.`

const IMPL_SCHEMA = {
  type: 'object',
  properties: {
    zusammenfassung: { type: 'string' },
    entscheidungen: { type: 'array', items: { type: 'string' }, description: 'bewusste Entscheidungen/Abweichungen von der Spezifikation mit Grund' },
    pruefbefehle: { type: 'string', description: 'Ergebnis der Pflicht-Prüfbefehle (Anzahl Tests etc.)' },
    offen: { type: 'array', items: { type: 'string' } },
  },
  required: ['zusammenfassung', 'entscheidungen', 'pruefbefehle', 'offen'],
}

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    befunde: { type: 'array', items: { type: 'object', properties: {
      schwere: { type: 'string', enum: ['hoch', 'mittel', 'niedrig'] },
      ort: { type: 'string', description: 'Datei:Zeile' },
      problem: { type: 'string' },
      vorschlag: { type: 'string' },
    }, required: ['schwere', 'ort', 'problem', 'vorschlag'] } },
    gesamt: { type: 'string' },
  },
  required: ['befunde', 'gesamt'],
}

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    behoben: { type: 'array', items: { type: 'string' } },
    nicht_behoben: { type: 'array', items: { type: 'string' }, description: 'mit Begründung' },
    pruefbefehle: { type: 'string' },
  },
  required: ['behoben', 'nicht_behoben', 'pruefbefehle'],
}

const { paket, spec } = args

phase('Umsetzung')
const impl = await agent(`${REGELN}

AUFTRAG: Setze ${paket} vollständig um. Spezifikation:
${spec}

Wo die Spezifikation etwas offen lässt, entscheide wie der vorhandene Code es tun würde und nenne die Entscheidung. Wenn etwas aus der Spezifikation im Code schon existiert, nutze es statt es neu zu bauen.`, { label: 'umsetzung', phase: 'Umsetzung', schema: IMPL_SCHEMA, model: 'sonnet', effort: 'high' })

phase('Prüfung')
const review = await agent(`Du bist unabhängige:r Senior-Reviewer:in (Staff Engineer) für dieses Repo und hast den Code NICHT geschrieben.
${REGELN}

Geprüft wird die uncommittete Änderung im Arbeitsbaum (git status, git diff, neue Dateien vollständig lesen) für ${paket}.
Spezifikation:
${spec}

Bericht der Umsetzung (nicht blind glauben):
${JSON.stringify(impl)}

Prüfe gründlich und belege mit Datei:Zeile:
1) Erfüllt es die Spezifikation vollständig? Fehlt etwas?
2) Korrektheit und Randfälle (Soft Delete, bestehende Zeilen, Nebenläufigkeit, Idempotenz, Zeitzonen).
3) Sicherheit: Rechteprüfung jeder neuen Route (IDOR), anonyme Routen (Ratenbegrenzung, Datenminimierung), Open Redirects, XSS, Aufzählbarkeit.
4) Datenschutz: Export/Purge, Verarbeitungsverzeichnis/Datenschutzerklärung wo nötig.
5) Regeln: i18n vollständig (jeder sichtbare Text + en.ts), Versionshinweis, Längengrenzen, keine unerlaubte Abhängigkeit, Mobile 375 px (keine festen Breiten, Umbruch), keine abgeschwächten Tests.
6) Qualität: unnötige Komplexität, Duplikate, toter Code, irreführende Kommentare, fehlende oder schwache Tests.
Führe die Pflicht-Prüfbefehle selbst aus und melde Fehlschläge als hoch. Ändere keinen Code. Melde nur echte Befunde, keine Geschmacksfragen.`, { label: 'pruefung', phase: 'Prüfung', schema: REVIEW_SCHEMA, model: 'sonnet', effort: 'high' })

const wichtig = (review && review.befunde ? review.befunde : []).filter(b => b.schwere !== 'niedrig')
let fix = null
if (wichtig.length > 0) {
  phase('Nachbesserung')
  fix = await agent(`${REGELN}

Im Arbeitsbaum liegt die uncommittete Umsetzung von ${paket}. Spezifikation:
${spec}

Ein unabhängiger Review hat diese Befunde geliefert:
${JSON.stringify(review.befunde)}

Behebe alle Befunde der Schwere hoch und mittel; niedrige nur, wenn trivial und klar richtig. Prüfe jeden Befund zuerst selbst - ist er falsch, begründe das unter nicht_behoben statt blind zu ändern. Danach alle Pflicht-Prüfbefehle.`, { label: 'nachbesserung', phase: 'Nachbesserung', schema: FIX_SCHEMA, model: 'sonnet', effort: 'high' })
} else {
  log('Keine Befunde hoch/mittel - Nachbesserung entfällt')
}

return { impl, review, fix }
