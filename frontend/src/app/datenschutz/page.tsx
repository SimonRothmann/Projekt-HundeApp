import type { Metadata } from "next";
import { MarketingFooter, MarketingHeader } from "@/components/marketing/marketing-chrome";
import {
  AUFSICHTSBEHOERDE,
  BETREIBER,
  HOSTING,
  NETZWERKDIENST,
  SICHERUNGSSPEICHER,
  SICHERUNG_AUFBEWAHRUNG,
  STAND,
} from "@/lib/rechtliches";

export const metadata: Metadata = {
  title: "Datenschutzerklärung",
  description:
    "Welche Daten Dogity speichert, warum, wie lange – und wie du Auskunft bekommst oder dein Konto löschst.",
  alternates: { canonical: "/datenschutz" },
};

function Abschnitt({ titel, children }: { titel: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-border/60 py-8">
      <h2 className="text-xl font-bold tracking-tight sm:text-2xl">{titel}</h2>
      <div className="mt-4 flex flex-col gap-3 text-sm leading-relaxed text-muted-foreground">{children}</div>
    </section>
  );
}

function Liste({ punkte }: { punkte: string[] }) {
  return (
    <ul className="flex list-disc flex-col gap-1.5 pl-5">
      {punkte.map((punkt) => (
        <li key={punkt}>{punkt}</li>
      ))}
    </ul>
  );
}

/**
 * Informationspflicht nach Art. 13 DSGVO.
 *
 * Der Text beschreibt, was die App TATSÄCHLICH tut - jede genannte Stelle ist
 * im Code nachweisbar (Kartenkacheln, Ortssuche, Wetterdienst, Speicherung im
 * Browser, Sicherungen). Kommt eine Datenverarbeitung dazu oder fällt eine
 * weg, gehört dieser Text in denselben Commit: Eine Datenschutzerklärung, die
 * etwas anderes behauptet als der Code tut, ist schlimmer als keine.
 */
export default function DatenschutzPage() {
  return (
    <div className="flex min-h-full min-w-0 flex-col">
      <MarketingHeader />

      <main className="mx-auto w-full max-w-3xl flex-1 px-4">
        <section className="py-12 sm:py-16">
          <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">Datenschutzerklärung</h1>
          <p className="mt-4 text-base text-muted-foreground">
            Dogity ist ein Trainingstagebuch. Damit es das sein kann, speichert es, was du einträgst – und sonst nichts.
            Es gibt keine Werbung, keine Analyse-Dienste, kein Tracking und keinen Weiterverkauf von Daten. Diese Seite
            beschreibt im Einzelnen, was gespeichert wird, warum, wie lange und welche Rechte du hast.
          </p>
        </section>

        <Abschnitt titel="1. Wer verantwortlich ist">
          <p className="text-foreground">
            {BETREIBER.name}
            <br />
            {BETREIBER.strasse}
            <br />
            {BETREIBER.plz} {BETREIBER.ort}
            <br />
            <a
              href={`mailto:${BETREIBER.email}`}
              className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
            >
              {BETREIBER.email}
            </a>
          </p>
          <p>
            Ein Datenschutzbeauftragter ist nicht bestellt; die gesetzlichen Voraussetzungen dafür liegen bei einem
            privat betriebenen Dienst dieser Größe nicht vor.
          </p>
        </Abschnitt>

        <Abschnitt titel="2. Welche Daten gespeichert werden">
          <p>Alles hier Genannte entsteht dadurch, dass du es einträgst oder die App benutzt:</p>
          <p className="font-medium text-foreground">Konto</p>
          <Liste
            punkte={[
              "Vor- und Nachname, E-Mail-Adresse",
              "Passwort – niemals im Klartext, sondern nur als nicht rückrechenbarer Hash",
              "optional eine Adresse zu einem Profilbild im Netz",
              "Zeitpunkt der Registrierung, Rolle (Mitglied, Trainer:in, Admin)",
            ]}
          />
          <p className="font-medium text-foreground">Hunde</p>
          <Liste punkte={["Name, Rasse, Geburtstag, Geschlecht, Notizen und – wenn du eines hochlädst – ein Foto"]} />
          <p className="font-medium text-foreground">Training</p>
          <Liste
            punkte={[
              "Datum, Uhrzeit und Dauer der Einheit",
              "Ort als Name und – wenn du ihn übernimmst – als Koordinaten",
              "Wetterdaten zu Ort und Zeitpunkt der Einheit",
              "geübte Übungen mit deiner Bewertung, Erfolg und Notizen",
              "Rückmeldungen deiner Trainer:innen zu einzelnen Übungen",
            ]}
          />
          <p className="font-medium text-foreground">Fährten (Standortdaten)</p>
          <Liste
            punkte={[
              "die aufgezeichnete Fährte als Folge einzelner GPS-Punkte mit Zeitstempel und Genauigkeit",
              "der Weg des Hundes beim Ablaufen, ebenso als Punktfolge",
              "daraus errechnete Auswertungen: Abweichung, gefundene Gegenstände, Stockungen",
            ]}
          />
          <p className="font-medium text-foreground">Verein und Gruppen</p>
          <Liste
            punkte={[
              "Mitgliedschaften und Beitrittsanfragen samt Status",
              "Trainerrollen, Gruppenzugehörigkeit und die Zuordnung zwischen Trainer:in und Hund",
              "geplante Gruppentrainings, an denen du teilnimmst oder die du leitest",
            ]}
          />
          <p className="font-medium text-foreground">Sonstiges</p>
          <Liste
            punkte={[
              "dein Lernfortschritt im Sachkunde-Fragentrainer",
              "Benachrichtigungen innerhalb der App",
              "deine Einstellungen: Sprache, Land, Schriftgröße, ausgeblendete Bereiche, gewählte Sportarten",
            ]}
          />
        </Abschnitt>

        <Abschnitt titel="3. Standortdaten – der empfindlichste Teil">
          <p>
            Beim Aufzeichnen einer Fährte liest die App den Standort deines Geräts aus. Das ergibt eine metergenaue Spur
            davon, wo du wann unterwegs warst – die persönlichste Angabe, die Dogity speichert. Deshalb gilt:
          </p>
          <Liste
            punkte={[
              "Die Aufzeichnung läuft nur, wenn du sie selbst startest, und nur so lange, wie sie sichtbar läuft.",
              "Dein Browser fragt dich vorher um Erlaubnis; du kannst sie jederzeit widerrufen.",
              "Die Punkte bleiben auf dem Server von Dogity. Sie gehen an keinen Kartendienst und an keinen Dritten.",
              "Die Fährte sehen nur du, Mitbesitzer:innen des Hundes und Trainer:innen, denen der Hund zugeordnet ist.",
              "Löschst du die Fährte oder dein Konto, verschwinden die Punkte mit.",
            ]}
          />
          <p>
            Beim Eintragen eines Trainingsorts kannst du den aktuellen Standort übernehmen. Das ist ein einzelner Punkt,
            keine Aufzeichnung – und freiwillig, du kannst den Ort auch von Hand eintippen.
          </p>
        </Abschnitt>

        <Abschnitt titel="4. Auf welcher Rechtsgrundlage">
          <Liste
            punkte={[
              "Konto, Hunde, Trainings, Fährten, Verein und Einstellungen: Art. 6 Abs. 1 lit. b DSGVO – ohne diese Daten gibt es die Funktion nicht, für die du dich angemeldet hast.",
              "Schutz vor Missbrauch, Fehlersuche und Sicherheit des Betriebs: Art. 6 Abs. 1 lit. f DSGVO. Mein berechtigtes Interesse ist, dass der Dienst erreichbar bleibt und Konten nicht übernommen werden.",
              "Das Auslesen deines Standorts erlaubst du im Browser ausdrücklich und kannst die Erlaubnis jederzeit zurücknehmen.",
            ]}
          />
        </Abschnitt>

        <Abschnitt titel="5. Speicherung auf deinem Gerät – warum es kein Cookie-Banner gibt">
          <p>
            Dogity selbst setzt keine Cookies. Die App legt einige Werte im lokalen Speicher deines Browsers ab, und
            zwar nur diese:
          </p>
          <Liste
            punkte={[
              "dein Anmelde-Token und der zugehörige Erneuerungs-Token – ohne sie wärst du bei jedem Seitenwechsel abgemeldet",
              "dein Name, deine E-Mail-Adresse und deine Rolle, damit die App sie beim Start nicht erst nachladen muss",
              "die zuletzt gewählte Kartenart, die Schriftgröße und welchen Neuerungs-Hinweis du schon gesehen hast",
              "noch nicht abgeschickte Einträge, solange du offline bist – sie gehen an den Server, sobald wieder Verbindung besteht",
            ]}
          />
          <p>
            Das alles dient ausschließlich dem Betrieb der App, die du selbst aufgerufen hast, und ist damit nach § 25
            Abs. 2 TDDDG ohne Einwilligung zulässig. Nichts davon verlässt dein Gerät in Richtung Dritter, nichts davon
            dient der Messung deines Verhaltens. Deshalb gibt es hier auch nichts zuzustimmen oder abzulehnen – ein
            Banner wäre eine Frage ohne Gegenstand. Abmelden oder das Löschen der Browserdaten entfernt diese Werte.
          </p>
          <p>
            Anders ist es beim vorgeschalteten Schutzdienst Cloudflare (siehe Abschnitt 6): Um automatisierte Angriffe
            von Menschen zu unterscheiden, kann er ein kleines Prüfskript in die Seite einfügen und ein kurzlebiges
            Sicherheits-Cookie setzen. Beides dient allein dem Schutz des Dienstes und ist nach § 25 Abs. 2 Nr. 2
            TDDDG ebenfalls ohne Einwilligung zulässig.
          </p>
        </Abschnitt>

        <Abschnitt titel="6. Server, Logdateien und Sicherungen">
          <p>
            Dogity läuft auf einem gemieteten Server bei {HOSTING.anbieter}. Standort der Server:{" "}
            {HOSTING.standort}. Mit dem Anbieter besteht ein Vertrag zur Auftragsverarbeitung nach Art. 28 DSGVO; er
            verarbeitet die Daten ausschließlich weisungsgebunden für den Betrieb.
          </p>
          <p>
            Vor dem Server steht {NETZWERKDIENST.anbieter} als Schutz- und Vermittlungsdienst. Jede Anfrage an Dogity
            läuft zuerst durch das Netz von Cloudflare: Dort wird die verschlüsselte Verbindung zu deinem Gerät
            aufgebaut, Angriffe und massenhafte automatisierte Zugriffe werden abgewehrt, und die Anfrage geht
            verschlüsselt weiter an den Server. Cloudflare verarbeitet dabei deine IP-Adresse und die übrigen
            Verbindungsdaten; weil die Verschlüsselung dort endet und neu aufgebaut wird, laufen auch die Inhalte der
            Anfragen durch dieses Netz. Rechtsgrundlage ist Art. 6 Abs. 1 lit. f DSGVO – mein berechtigtes Interesse ist
            ein Dienst, der erreichbar bleibt und Angriffe abhält. Mit Cloudflare besteht ein Vertrag zur
            Auftragsverarbeitung nach Art. 28 DSGVO.
          </p>
          <p>
            Cloudflare hat seinen Sitz in den {NETZWERKDIENST.sitz}. Bearbeitet werden Anfragen in der Regel in einem
            Rechenzentrum in deiner Nähe; eine Verarbeitung in den USA lässt sich aber nicht ausschließen. Cloudflare
            ist nach dem EU-US Data Privacy Framework zertifiziert, für das die EU-Kommission ein angemessenes
            Datenschutzniveau festgestellt hat (Art. 45 DSGVO). Außerdem kann Cloudflare deinen Browser bitten,
            fehlgeschlagene Verbindungen direkt an Cloudflare zu melden (Network Error Logging), und E-Mail-Adressen auf
            der Seite in eine verschlüsselte Form bringen, die erst dein Browser wieder lesbar macht – als Schutz vor
            Adresssammlern.
          </p>
          <p>
            Beim Aufruf der Seite fallen technisch notwendige Verbindungsdaten an – IP-Adresse, Zeitpunkt, aufgerufene
            Adresse, Browserkennung. Sie dienen dem Betrieb und der Abwehr von Angriffen (Art. 6 Abs. 1 lit. f DSGVO)
            und werden nicht mit deinem Konto zusammengeführt oder zu Auswertungen verwendet. Um das massenhafte
            Ausprobieren von Passwörtern zu begrenzen, zählt der Server außerdem kurzzeitig Anmeldeversuche je
            IP-Adresse.
          </p>
          <p>
            Von der Datenbank wird täglich eine Sicherung erstellt. Sie wird verschlüsselt, bevor sie den Server
            verlässt – und zwar so, dass auf dem Server nur der Schlüssel zum Verschlüsseln liegt, nicht der zum
            Öffnen. Wer sich Zugang zum Server verschafft, kann die Sicherungen deshalb nicht lesen. Abgelegt werden
            sie auf dem Server selbst und zusätzlich bei {SICHERUNGSSPEICHER.anbieter} in einem Speicher, der fest auf
            die {SICHERUNGSSPEICHER.jurisdiktion} festgelegt ist. Auch mit diesem Anbieter besteht ein Vertrag zur
            Auftragsverarbeitung nach Art. 28 DSGVO.
          </p>
          <p>
            Aufbewahrt wird gestaffelt: {SICHERUNG_AUFBEWAHRUNG.lokal} Tage auf dem Server, ausgelagert{" "}
            {SICHERUNG_AUFBEWAHRUNG.taeglich} Tage für die täglichen Stände, {SICHERUNG_AUFBEWAHRUNG.woechentlich} Tage
            für die wöchentlichen und {SICHERUNG_AUFBEWAHRUNG.monatlich} Tage für die monatlichen. Löschst du etwas,
            ist es sofort aus der laufenden App verschwunden; aus den Sicherungen fällt es mit dem jeweiligen Stand
            heraus, im äußersten Fall also nach gut dreizehn Monaten. Sicherungen werden ausschließlich zur
            Wiederherstellung nach einem Ausfall verwendet.
          </p>
        </Abschnitt>

        <Abschnitt titel="7. Dienste Dritter">
          <p>Dogity bindet keine Analyse-, Werbe- oder Social-Media-Dienste ein. Diese Stellen sind beteiligt:</p>
          <p className="font-medium text-foreground">Kartenbilder (in deinem Browser)</p>
          <p>
            Die Karten bestehen aus Kacheln, die dein Gerät direkt beim Kartenanbieter abruft. Dabei erfährt dieser
            technisch bedingt deine IP-Adresse und den gezeigten Kartenausschnitt. Standard ist die Karte der{" "}
            <span className="text-foreground">OpenStreetMap Foundation</span> (Vereinigtes Königreich). Schaltest du auf
            das Luftbild um, kommen die Kacheln von <span className="text-foreground">Esri</span>; dabei werden Daten in
            die USA übertragen. Das Luftbild wird nur geladen, wenn du es selbst auswählst – die Karte startet immer mit
            OpenStreetMap.
          </p>
          <p className="font-medium text-foreground">Ortssuche und Wetter (vom Server, nicht von deinem Gerät)</p>
          <p>
            Suchst du einen Trainingsort, fragt der Server von Dogity den Dienst{" "}
            <span className="text-foreground">Photon</span> (komoot GmbH, Berlin). Für die Temperatur beim Fährtenlegen
            und -suchen fragt er <span className="text-foreground">Open-Meteo</span>. In beiden Fällen geht nur die
            Anfrage selbst hinaus – Suchbegriff beziehungsweise Koordinaten und Zeitpunkt. Deine IP-Adresse und dein Name
            werden nicht übermittelt; die Dienste sehen nur den Server von Dogity.
          </p>
          <p className="font-medium text-foreground">Ko-fi</p>
          <p>
            Die Seite verweist auf eine Spendenseite bei Ko-fi. Das ist ein gewöhnlicher Verweis, kein eingebettetes
            Fenster und kein Skript: Es geht nichts an Ko-fi, solange du nicht selbst darauf tippst. Was dort passiert,
            richtet sich nach den Bestimmungen von Ko-fi.
          </p>
        </Abschnitt>

        <Abschnitt titel="8. Wer deine Daten sonst sieht">
          <p>Innerhalb von Dogity sehen andere Menschen nur, was die Zusammenarbeit erfordert:</p>
          <Liste
            punkte={[
              "Mitbesitzer:innen, die du selbst zu einem Hund hinzufügst, sehen dessen Tagebuch vollständig.",
              "Trainer:innen einer Gruppe, der du selbst beigetreten bist oder deren Einladung du angenommen hast, können deine Hunde betreuen. Sie sehen dann deren Trainings, Ziele und Fährten und können Rückmeldung geben. Du wirst benachrichtigt, sobald jemand einen deiner Hunde betreut; verlässt du die Gruppe, endet die Betreuung.",
              "Die Vereinsverwaltung sieht Mitgliedschaften, Gruppenzugehörigkeit und Namen ihrer Mitglieder – nicht deren Trainingstagebuch.",
              "Der Betreiber kann aus technischen Gründen auf die Datenbank zugreifen, tut das aber nur zur Fehlersuche und zum Betrieb.",
            ]}
          />
          <p>
            Eine Weitergabe an Dritte zu Werbe- oder Auswertungszwecken findet nicht statt. Ein Verkauf ebenso wenig.
            Herausgabe an Behörden nur, soweit ich gesetzlich dazu verpflichtet bin.
          </p>
        </Abschnitt>

        <Abschnitt titel="9. Wie lange gespeichert wird">
          <Liste
            punkte={[
              "Kontodaten und alles, was daran hängt: bis du das Konto löschst.",
              "Einzelne Einträge – Trainings, Fährten, Hunde: bis du sie löschst.",
              "Anmelde-Token auf dem Server: der kurzlebige nach einer Stunde, der Erneuerungs-Token spätestens nach 60 Tagen.",
              `Sicherungen der Datenbank: ${SICHERUNG_AUFBEWAHRUNG.lokal} Tage auf dem Server; ausgelagert ${SICHERUNG_AUFBEWAHRUNG.taeglich} Tage (tägliche Stände), ${SICHERUNG_AUFBEWAHRUNG.woechentlich} Tage (wöchentliche) und ${SICHERUNG_AUFBEWAHRUNG.monatlich} Tage (monatliche).`,
            ]}
          />
          <p>
            Löschst du dein Konto, werden dein Zugang, deine Hunde, Trainings, Fährten, Ziele, Einstellungen, dein
            Lernfortschritt und deine Vereins- und Gruppenzugehörigkeiten entfernt. Hunde, die du dir mit jemandem
            teilst, bleiben bei der anderen Person – nur deine Verknüpfung damit verschwindet. Wo du als Trainer:in
            Inhalte für einen Verein angelegt hast, bleiben diese Inhalte dem Verein erhalten, die Angabe deiner
            Urheberschaft wird aber entfernt.
          </p>
        </Abschnitt>

        <Abschnitt titel="10. Deine Rechte">
          <p>Nach der DSGVO stehen dir zu:</p>
          <Liste
            punkte={[
              "Auskunft über die zu dir gespeicherten Daten (Art. 15)",
              "Berichtigung falscher Daten (Art. 16)",
              "Löschung (Art. 17)",
              "Einschränkung der Verarbeitung (Art. 18)",
              "Herausgabe deiner Daten in einem übertragbaren Format (Art. 20)",
              "Widerspruch gegen Verarbeitungen, die auf berechtigtem Interesse beruhen (Art. 21)",
            ]}
          />
          <p className="text-foreground">
            Für Auskunft und Löschung musst du niemandem schreiben: Unter Profil findest du „Meine Daten herunterladen“
            – das liefert dir alles Gespeicherte als Datei – und „Konto löschen“, das dein Konto samt Daten sofort
            entfernt.
          </p>
          <p>
            Für alles Übrige genügt eine Nachricht an{" "}
            <a
              href={`mailto:${BETREIBER.email}`}
              className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
            >
              {BETREIBER.email}
            </a>
            .
          </p>
          <p>
            Unabhängig davon kannst du dich bei einer Datenschutz-Aufsichtsbehörde beschweren (Art. 77 DSGVO). Für mich
            zuständig ist:
          </p>
          <p className="text-foreground">
            {AUFSICHTSBEHOERDE.name}
            <br />
            {AUFSICHTSBEHOERDE.strasse}
            <br />
            {AUFSICHTSBEHOERDE.plz} {AUFSICHTSBEHOERDE.ort}
          </p>
        </Abschnitt>

        <Abschnitt titel="11. Sicherheit">
          <p>
            Die Verbindung ist durchgehend mit TLS verschlüsselt, auch zwischen Cloudflare und dem Server. Passwörter
            werden nur als Hash gespeichert. Anmeldungen laufen über kurzlebige Token, die sich serverseitig widerrufen
            lassen; wird ein bereits verbrauchter Token erneut vorgelegt, werden alle Sitzungen des Kontos beendet,
            und wer sein Passwort ändert oder zurücksetzt, meldet damit alle anderen Geräte ab. Nach fünf falschen
            Passwörtern in Folge wird das Konto für einige Minuten gesperrt. Der Browser darf Inhalte nur
            aus festgelegten Quellen laden (Content-Security-Policy), und Anmeldeversuche sind je IP-Adresse begrenzt.
          </p>
        </Abschnitt>

        <Abschnitt titel="12. Automatisierte Entscheidungen">
          <p>
            Eine automatisierte Entscheidung mit rechtlicher Wirkung findet nicht statt. Dogity erstellt allerdings aus
            deinen eigenen Bewertungen einen Trainingsvorschlag: Übungen, die du schwächer bewertest, plant es häufiger
            ein. Das ist ein Vorschlag, den du jederzeit ändern oder ignorieren kannst.
          </p>
        </Abschnitt>

        <Abschnitt titel="13. Kinder und Jugendliche">
          <p>
            Dogity richtet sich an Erwachsene. Wer jünger als 16 Jahre ist, sollte ein Konto nur mit Zustimmung der
            Eltern anlegen. Erfahre ich, dass ein Konto ohne diese Zustimmung besteht, lösche ich es.
          </p>
        </Abschnitt>

        <Abschnitt titel="14. Änderungen">
          <p>
            Kommt eine Funktion dazu, die Daten anders verarbeitet, ändert sich dieser Text mit. Über nennenswerte
            Änderungen informiert die Seite{" "}
            <a href="/neuerungen" className="text-primary-text underline-offset-4 hover:underline">
              Neuerungen
            </a>
            .
          </p>
        </Abschnitt>

        <p className="border-t border-border/60 py-8 text-xs text-muted-foreground">Stand: {STAND}</p>
      </main>

      <MarketingFooter />
    </div>
  );
}
