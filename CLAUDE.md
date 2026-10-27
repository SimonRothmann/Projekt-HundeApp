# CLAUDE.md

Arbeitsweise für KI-Assistenten in diesem Repo. Die Projektregeln stehen in
[AI_RULES.md](AI_RULES.md) und [CODING_GUIDELINES.md](CODING_GUIDELINES.md) -
sie gehen vor, wo sie etwas Genaueres sagen.

Die vier Grundsätze unten sind die Leitlinien aus
[andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills)
(MIT, nach Andrej Karpathys Beobachtungen zu typischen Fehlern von
KI-Assistenten beim Programmieren), übersetzt und an Dogity angepasst. Die
englische Originalfassung liegt als Skill unter
`.claude/skills/karpathy-guidelines/`.

**Das Repo ist öffentlich.** Sicherheitslücken erst beheben, dann beschreiben - keine Angriffswege in Doku, TODO oder Commit-Nachrichten, solange die Lücke offen ist. Keine Geheimnisse, Zugangsdaten oder `.env`-Inhalte einchecken.

**Abwägung:** Die Grundsätze bremsen zugunsten von Sorgfalt. Bei trivialen
Aufgaben mit Augenmaß anwenden.

## 1. Erst denken, dann programmieren

**Nichts unterstellen. Unklarheit nicht verstecken. Abwägungen offenlegen.**

- Annahmen ausdrücklich nennen.
- Gibt es mehrere Lesarten, sie nennen - nicht stillschweigend eine wählen.
- Gibt es einen einfacheren Weg, ihn sagen. Widersprechen, wo es angebracht ist.
- Ist etwas unklar: anhalten, benennen, was unklar ist.

**Für Dogity:** Fragen gehören an den Betreiber nur bei echten
Produktentscheidungen (was Nutzer:innen sehen, Rechte, Datenschutz, Inhalte von
Verbänden). Alles andere mit Empfehlung selbst entscheiden und die Entscheidung
nennen. Sub-Worker in Workflows können niemanden fragen: Sie entscheiden
begründet und führen die Entscheidung im Bericht unter "Entscheidungen" bzw.
"Offen" auf - nie stillschweigend.

## 2. Einfachheit zuerst

**So wenig Code wie nötig. Nichts auf Vorrat.**

- Keine Funktionen über den Auftrag hinaus.
- Keine Abstraktionen für Code, der nur einmal gebraucht wird.
- Keine "Flexibilität" oder Konfigurierbarkeit, die niemand verlangt hat.
- Keine Fehlerbehandlung für Fälle, die nicht eintreten können.
- Werden es 200 Zeilen, wo 50 reichen: neu schreiben.

Prüffrage: Würde eine erfahrene Entwickler:in das überladen nennen? Dann
vereinfachen.

## 3. Chirurgische Änderungen

**Nur anfassen, was sein muss. Nur eigenen Dreck wegräumen.**

- Benachbarten Code, Kommentare und Formatierung nicht "verbessern".
- Nichts umbauen, was nicht kaputt ist.
- Den vorhandenen Stil treffen, auch wenn man es anders machen würde.
- Fremden toten Code erwähnen, nicht löschen.
- Was durch die EIGENE Änderung ungenutzt wird (Importe, Variablen,
  Funktionen), entfernen.

Prüffrage: Lässt sich jede geänderte Zeile direkt auf den Auftrag zurückführen?

## 4. Zielgerichtet arbeiten

**Erfolgskriterien festlegen. Wiederholen, bis es nachweislich stimmt.**

Aufgaben in prüfbare Ziele übersetzen:

- "Validierung ergänzen" → Tests für ungültige Eingaben schreiben, dann grün machen.
- "Fehler beheben" → Test, der den Fehler zeigt, dann grün machen.
- "X umbauen" → Tests vorher und nachher grün.

Bei mehrschrittigen Aufgaben ein kurzer Plan:

```
1. [Schritt] → prüfen: [Kontrolle]
2. [Schritt] → prüfen: [Kontrolle]
```

**Für Dogity:** "Nachweislich" heißt mindestens die Pflicht-Prüfbefehle
(`dotnet build`, `dotnet test`, `npx tsc --noEmit`, `npm run lint`,
`npx vitest run`, `npx next build`) und bei Oberflächenänderungen ein Blick bei
375 px Breite ohne horizontalen Scroll. Tests werden nie abgeschwächt, um grün
zu werden.

---

Die Grundsätze wirken, wenn Diffs weniger unnötige Änderungen enthalten, weniger
wegen Überladung neu geschrieben werden muss und Rückfragen vor der Umsetzung
kommen statt nach dem Fehler.
