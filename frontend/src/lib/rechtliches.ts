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
 * Wo die Server stehen. Hetzner betreibt Standorte in Deutschland, Finnland
 * und den USA - für die Datenschutzerklärung ist der Unterschied wesentlich,
 * weil außerhalb der EU zusätzliche Angaben fällig würden.
 */
export const HOSTING = {
  anbieter: "Hetzner Online GmbH, Industriestr. 25, 91710 Gunzenhausen",
  standort: "Deutschland",
} as const;

/**
 * Wie lange die täglichen Datenbanksicherungen aufbewahrt werden. Steht hier,
 * weil die Datenschutzerklärung eine Zahl nennen muss: Gelöschte Daten sind
 * erst dann wirklich fort, wenn auch die letzte Sicherung mit ihnen abgelaufen
 * ist. Der passende Aufräumbefehl steht in deploy/README.md - die Zahl hier
 * und der Cronjob dort müssen zusammenpassen.
 */
export const SICHERUNG_AUFBEWAHRUNG_TAGE = 30;

/** Letzte inhaltliche Änderung an Impressum oder Datenschutzerklärung. */
export const STAND = "2026-09-13";

/** "Hauptstr. 20/2, 76307 Karlsbad" - für Fließtext und strukturierte Daten. */
export const ANSCHRIFT_EINZEILIG = `${BETREIBER.strasse}, ${BETREIBER.plz} ${BETREIBER.ort}`;
