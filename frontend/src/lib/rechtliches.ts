/**
 * Die Angaben zum Betreiber - an einer Stelle.
 *
 * Sie stehen im Impressum, in der Datenschutzerklärung und in der
 * Kontaktzeile. Gepflegt an drei Orten liefen sie auseinander, und beim
 * Impressum ist das kein Schönheitsfehler: Eine unvollständige oder falsche
 * Anbieterkennzeichnung ist abmahnfähig (§ 5 DDG).
 *
 * WICHTIG: Ändert sich hier etwas, gehört das Datum in STAND mit angefasst.
 */
export const BETREIBER = {
  name: "Simon Rothmann",
  // Ladungsfähige Anschrift - ein Postfach genügt dem Gesetz nicht.
  strasse: "Hauptstr. 20/2",
  plz: "76307",
  ort: "Karlsbad",
  land: "Deutschland",
  /**
   * Die private Adresse des Betreibers.
   *
   * Sie steht damit öffentlich im Netz und wird von Adresssammlern gefunden.
   * Wer das ändern will, richtet beim Domain-Anbieter eine Weiterleitung für
   * kontakt@dogity.net ein und trägt sie hier ein - eine Zeile, und weder
   * Impressum noch Datenschutzerklärung müssen angefasst werden.
   */
  email: "simonrothmann72@gmail.com",
} as const;

/**
 * Zuständig ist die Behörde am Sitz des Verantwortlichen - bei Karlsbad
 * (Landkreis Karlsruhe) also Baden-Württemberg. Muss in der
 * Datenschutzerklärung stehen, damit das Beschwerderecht aus Art. 77 DSGVO
 * nicht nur behauptet, sondern auch nutzbar ist.
 */
export const AUFSICHTSBEHOERDE = {
  name: "Der Landesbeauftragte für den Datenschutz und die Informationsfreiheit Baden-Württemberg",
  strasse: "Lautenschlagerstraße 20",
  plz: "70173",
  ort: "Stuttgart",
  web: "https://www.baden-wuerttemberg.datenschutz.de",
} as const;

/**
 * Wer die Server betreibt und wo sie stehen.
 *
 * Am 2026-09-13 von Hetzner auf Contabo berichtigt. Mehrere Entwurfsdokumente
 * im Repo (DEPLOYMENT.md, TECH_STACK.md) nannten noch den Anbieter aus der
 * Planungsphase; daraufhin stand in der veröffentlichten
 * Datenschutzerklärung der falsche Auftragsverarbeiter. Ein Name, der nicht
 * stimmt, macht die Angabe wertlos - hier gegen die Wirklichkeit prüfen, nicht
 * gegen die Planungsunterlagen.
 */
export const HOSTING = {
  anbieter: "Contabo GmbH, München",
  standort: "Deutschland",
} as const;

/**
 * Wer VOR dem Server steht: Alle vier Domains laufen über Cloudflare als
 * Proxy (die DNS-Namen zeigen auf Cloudflare, nicht auf den Server; die
 * Antworten tragen cf-ray). Nachgetragen am 2026-09-21 - die erste Fassung
 * nannte Cloudflare nur als Sicherungsspeicher, dabei läuft jede Anfrage
 * samt Passwort beim Anmelden durch dieses Netz.
 *
 * Cloudflare fügt außerdem von sich aus Dinge hinzu, die in der
 * Datenschutzerklärung stehen müssen, solange sie im Dashboard eingeschaltet
 * sind: ein Prüfskript gegen Bots (/cdn-cgi/challenge-platform), die
 * Verschleierung von E-Mail-Adressen und Network Error Logging (Kopfzeilen
 * NEL/Report-To). Schaltet man sie ab, darf der Text bleiben - er sagt
 * "kann".
 */
export const NETZWERKDIENST = {
  anbieter: "Cloudflare, Inc.",
  sitz: "USA",
} as const;

/**
 * Wohin die verschlüsselten Datenbanksicherungen ausgelagert werden.
 *
 * Eigener Auftragsverarbeiter neben dem Hoster und deshalb eigene Angabe: In
 * den Sicherungen stecken dieselben personenbezogenen Daten wie in der
 * Datenbank. Der Speicher ist fest auf die EU-Jurisdiktion gestellt (der
 * Endpunkt trägt ".eu."); das lässt sich nachträglich nicht mehr ändern.
 */
export const SICHERUNGSSPEICHER = {
  anbieter: "Cloudflare, Inc.",
  jurisdiktion: "Europäische Union",
} as const;

/**
 * Wie lange Sicherungen aufbewahrt werden, in Tagen - gestaffelt nach dem
 * Großvater-Vater-Sohn-Muster aus docs/BACKUP.md.
 *
 * Die Datenschutzerklärung muss Fristen nennen können: Gelöschte Daten sind
 * erst dann wirklich fort, wenn auch der letzte Stand mit ihnen abgelaufen
 * ist. Gelöscht wird über Lifecycle-Regeln im Speicher, nicht vom
 * Sicherungsskript - diese Zahlen und die Regeln dort müssen zusammenpassen.
 */
export const SICHERUNG_AUFBEWAHRUNG = {
  lokal: 14,
  taeglich: 14,
  woechentlich: 70,
  monatlich: 400,
} as const;

/** Letzte inhaltliche Änderung an Impressum oder Datenschutzerklärung. */
export const STAND = "2026-09-21";

/** "Hauptstr. 20/2, 76307 Karlsbad" - für Fließtext und strukturierte Daten. */
export const ANSCHRIFT_EINZEILIG = `${BETREIBER.strasse}, ${BETREIBER.plz} ${BETREIBER.ort}`;
