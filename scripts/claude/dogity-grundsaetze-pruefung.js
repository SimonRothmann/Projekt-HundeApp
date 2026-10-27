export const meta = {
  name: 'dogity-grundsaetze-pruefung',
  description: 'Ganze Dogity-App nach den vier Grundsätzen aus CLAUDE.md prüfen (nur lesen, Bericht)',
  phases: [
    { title: 'Prüfen', detail: 'vier Bereiche parallel, jeder prüft seine Befunde selbst gegen', model: 'sonnet' },
  ],
}

const REPO = '/Users/Simon.Rothmann/Documents/Workspace/Privat/Projekt-HundeApp'

const GEMEINSAM = `Du prüfst einen Bereich der Hundesport-App Dogity im Repo ${REPO} (Branch master; Next.js-Frontend in frontend/, .NET-10-Backend in backend/) gegen die vier Grundsätze in ${REPO}/CLAUDE.md. Lies CLAUDE.md zuerst. Die Grundsätze sind für Änderungen geschrieben; auf vorhandenen Code angewandt heißt das:

1. EINFACHHEIT: überladene Konstruktionen (Abstraktion/Interface/Helfer, den nur eine Stelle nutzt und der nichts kapselt), Konfigurierbarkeit oder Parameter, die nie anders als mit einem Wert benutzt werden, Fehlerbehandlung für Fälle, die nicht eintreten können, toter Code (nie aufgerufene Methoden, Endpunkte ohne Aufrufer im Frontend, ungenutzte Exporte, Komponenten, DTO-Felder, die niemand liest), Dateien/Funktionen, die mit deutlich weniger Code dasselbe täten, DIESELBE Regel an zwei Stellen unterschiedlich definiert (Drift-Gefahr).
2. CHIRURGISCH (Spuren früherer Änderungen): Überreste von Umbauten - verwaiste Hilfsfunktionen, auskommentierter Code, Kommentare, die etwas anderes beschreiben, als der Code tut, alte Namen/Begriffe, die nach einer Umbenennung stehen geblieben sind.
3. ERST DENKEN (versteckte Annahmen): stillschweigende Annahmen ohne Begründung (magische Zahlen/Schwellen ohne Kommentar, wo der Wert eine Produktentscheidung ist), Fehler, die verschluckt werden (leere catch-Blöcke, Fallbacks, die einen echten Fehler verdecken), Stellen, an denen eine mehrdeutige Regel still festgelegt wurde, Doku, die dem Code widerspricht.
4. ZIELGERICHTET (Nachweis): fachliche Regeln, Rechteprüfungen oder Berechnungen OHNE Test; Tests, die nichts prüfen (nur Aufruf ohne aussagekräftige Assertion, testen den Mock statt den Code); Schutzregeln, die nur im Frontend durchgesetzt werden.

NICHT melden: bewusst getroffene, im Code oder in docs/*.md begründete Entscheidungen (die führst du unter "bewusst_so" auf, wenn sie auf den ersten Blick wie ein Befund aussehen); Stilfragen; Geschmack; Wünsche nach neuen Features; generierter Code (Migrations/, *.Designer.cs, ModelSnapshot, Seed/Data/*.json, node_modules, .next). Seeder mit Katalogtexten: nur die LOGIK prüfen, nicht die Inhalte der Texte.

Regeln: NUR LESEN. Keine Datei ändern, nichts committen, keine Server starten, kein Netzwerk. Erlaubt: grep/find/lesen, \`git log\`/\`git blame\`, und wo es einen Befund belegt: \`cd ${REPO}/frontend && npx tsc --noEmit --noUnusedLocals --noUnusedParameters\` (nur lesen, ändert nichts), \`npx vitest run <datei>\`, \`cd ${REPO}/backend && dotnet build\` / \`dotnet test --filter ...\`. Arbeite effizient: Starte mit automatischen Signalen (ungenutzte Exporte per grep nach Aufrufern, größte Dateien, doppelte Hilfsfunktionen, catch-Blöcke, Endpunkte ohne Frontend-Aufrufer), dann gezielt lesen. Du kannst nicht alles Zeile für Zeile lesen - priorisiere zentrale und große Dateien und sag unter "geprueft", was du dir angesehen hast.

Prüfe JEDEN Befund selbst gegen, bevor du ihn meldest: Stelle erneut lesen, aktiv nach einem Grund suchen, warum es doch richtig ist (Aufrufer per grep über das GANZE Repo inkl. Tests und Frontend/Backend-Grenze, Reflection/Routing/DI, Begründung in Kommentar oder docs/). Melde nur, was standhält. Lieber wenige belastbare Befunde als viele weiche. Höchstens 15, nach Wirkung sortiert (Drift-Gefahr, verdeckte Fehler, ungetestete Rechte zuerst; kosmetischer toter Code zuletzt).`

const BEREICHE = [
  { key: 'backend-kern', prompt: `${GEMEINSAM}

Dein Bereich: backend/src/Dogity.Domain und backend/src/Dogity.Application (Services, reine Regelklassen, DTOs) samt zugehörigen Tests in backend/tests. Besonders: doppelt definierte Regeln (z. B. Rechte "darf Hund sehen/bearbeiten", Wochen-/Datumsrechnung, Vereinszeit/heute, Zählungen), Services mit sehr langen Methoden, Interfaces mit genau einer Implementierung ohne Testnutzen, ungetestete Rechteprüfungen in Services.` },
  { key: 'backend-rand', prompt: `${GEMEINSAM}

Dein Bereich: backend/src/Dogity.Infrastructure (Configurations, Seeder-LOGIK, Dienste, DependencyInjection) und backend/src/Dogity.Api (Controller, Hosting, Hintergrunddienste, Program.cs, Ratenbegrenzung). Besonders: Endpunkte ohne Aufrufer im Frontend (grep in ${REPO}/frontend/src nach dem Pfad), Hintergrunddienste mit verschluckten Fehlern, Konfigurationswerte ohne Begründung, Seeder-Logik mit Sonderfällen. BEKANNTER HINWEIS ZUM PRÜFEN: Ein Sub-Worker berichtete, SportCatalogSeeder.SeedSkgAgilityAsync (SportCatalogSeeder.Schweiz.cs) stürze auf einer FRISCHEN Postgres-Datenbank mit "value too long for type character varying(2000)" (regulations.Description) ab, obwohl SchweizKatalogTests die Länge <= 2000 prüft. Kläre per Lesen, ob und welcher Text/welche Spalte das sein kann (z. B. andere Spalte, anderer Pfad beim frischen Seeden, Zeichen vs. Längenzählung) - melde es nur, wenn du einen konkreten Mechanismus findest, sonst unter "geprueft" mit deiner Einschätzung.` },
  { key: 'frontend-komponenten', prompt: `${GEMEINSAM}

Dein Bereich: frontend/src/components (171 Dateien). Besonders: Komponenten, die nirgends importiert werden, fast identische Komponenten/Hilfsfunktionen an mehreren Stellen (Datumsformatierung, Sheets, Formularfelder, Statusanzeigen), sehr große Komponenten mit Logik, die in lib/ gehört und dort nicht getestet ist, Props/Varianten, die nie anders genutzt werden, verschluckte Fehler bei API-Aufrufen (catch ohne Rückmeldung an die Nutzer:in), Rechte- oder Validierungsregeln, die nur hier im Frontend stehen.` },
  { key: 'frontend-seiten-lib-doku', prompt: `${GEMEINSAM}

Dein Bereich: frontend/src/app (Seiten) und frontend/src/lib (Hilfsbibliotheken, api.ts, i18n) samt Tests, PLUS Abgleich Doku gegen Code: README.md, ARCHITECTURE.md, TECH_STACK.md, DEPLOYMENT.md, DATABASE.md, TODO.md (Abschnitt "Nächste Features" und offene Punkte) und docs/*.md - wo behauptet die Doku etwas, das der Code nicht (mehr) tut (z. B. "geplant", obwohl umgesetzt; alte Hoster/Namen; Endpunkte/Tabellen, die es nicht gibt)? Bei der Doku nur echte Widersprüche, keine Formulierungsfragen; nenne Datei und Abschnitt. Besonders im Code: ungenutzte Exporte in lib/ (grep nach Importen), doppelte Logik zwischen lib/ und Backend, die driften kann (z. B. Bewertungs-/Fälligkeitsregeln, die Frontend und Backend beide rechnen), Tests ohne aussagekräftige Assertion.` },
]

const BEFUNDE = {
  type: 'object',
  properties: {
    befunde: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          grundsatz: { type: 'string', enum: ['Einfachheit', 'Chirurgisch', 'Erst denken', 'Zielgerichtet'] },
          titel: { type: 'string' },
          ort: { type: 'string', description: 'Datei:Zeile, ggf. mehrere' },
          schwere: { type: 'string', enum: ['hoch', 'mittel', 'niedrig'] },
          beleg: { type: 'string', description: 'Was genau, mit konkretem Beleg (Aufrufer-Suche, Codezeile, Widerspruch)' },
          gegenpruefung: { type: 'string', description: 'Warum es nach aktiver Suche nach Gegengründen ein Befund bleibt' },
          vorschlag: { type: 'string' },
          aufwand: { type: 'string', enum: ['klein', 'mittel', 'groß'] },
          risiko: { type: 'string', description: 'Was beim Beheben kaputtgehen könnte' },
        },
        required: ['grundsatz', 'titel', 'ort', 'schwere', 'beleg', 'gegenpruefung', 'vorschlag', 'aufwand', 'risiko'],
      },
    },
    bewusst_so: { type: 'array', items: { type: 'string' }, description: 'Sieht aus wie ein Befund, ist aber begründet so (mit Fundstelle der Begründung)' },
    geprueft: { type: 'array', items: { type: 'string' }, description: 'Was angesehen wurde (Dateien/Bereiche), auch ohne Befund' },
    gesamteindruck: { type: 'string' },
  },
  required: ['befunde', 'bewusst_so', 'geprueft', 'gesamteindruck'],
}

phase('Prüfen')
const ergebnisse = await parallel(BEREICHE.map(b => () =>
  agent(b.prompt, { label: `prüfen:${b.key}`, phase: 'Prüfen', schema: BEFUNDE, model: 'sonnet' })
    .then(r => r ? { bereich: b.key, ...r } : null)))

return ergebnisse.filter(Boolean)
