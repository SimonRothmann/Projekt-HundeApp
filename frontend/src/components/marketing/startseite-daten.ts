import {
  BookOpenCheck,
  CalendarRange,
  CloudSun,
  Dog,
  Footprints,
  NotebookPen,
  Users,
  WifiOff,
} from "lucide-react";
import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Alles hier ist durch tatsächlich vorhandene Funktionen gedeckt - bewusst
 * keine Versprechen auf Vorrat. Eine Startseite, die mehr behauptet als die
 * App kann, bringt Besucher, die sofort wieder gehen; Suchmaschinen werten
 * genau das ab.
 *
 * Die Sätze sind mit uebersetzbar() markiert und werden erst in
 * startseite-inhalt.tsx übersetzt. Die strukturierten Daten (JSON-LD) in
 * page.tsx lesen dieselben Felder unübersetzt - die Suchmaschine bekommt
 * bewusst die deutsche Fassung.
 */
export const FEATURES = [
  {
    icon: NotebookPen,
    title: uebersetzbar("Trainingstagebuch"),
    text: uebersetzbar("Jede Einheit mit Übungen, Bewertung und Notizen festhalten - in wenigen Sekunden, direkt auf dem Hundeplatz. Trainer können Rückmeldung zu einzelnen Übungen geben."),
  },
  {
    icon: Footprints,
    title: uebersetzbar("Fährten per GPS aufzeichnen"),
    text: uebersetzbar("Gelegte Fährte und Ablauf des Hundes aufzeichnen und übereinanderlegen. Die Auswertung zeigt die Abweichung von der Fährte, gefundene Gegenstände und Stockungen - und unterscheidet dabei, ob der Hund an einem Gegenstand verweist oder wirklich sucht."),
  },
  {
    icon: CloudSun,
    title: uebersetzbar("Wetter automatisch"),
    text: uebersetzbar("Temperatur beim Legen und beim Suchen samt Änderung dazwischen. Genau die bestimmt maßgeblich, wie sich die Geruchsspur hält - ermittelt wird sie ohne eine einzige Eingabe."),
  },
  {
    icon: CalendarRange,
    title: uebersetzbar("Trainingsplan, der mitdenkt"),
    text: uebersetzbar("Aus Prüfungstermin und Ziel entsteht ein Wochenplan, der schwache Übungen häufiger einplant und sitzende seltener. Was mehr geübt werden soll, lässt sich von Hand höher gewichten."),
  },
  {
    icon: BookOpenCheck,
    title: uebersetzbar("Prüfungsordnungen hinterlegt"),
    text: uebersetzbar("BH, IBGH 1-3, IGP 1-3 sowie FPr, GPr, SPr, StöPr und UPr mit ihren Übungen und Punkten - als Grundlage für Ziele und Trainingsplanung."),
  },
  {
    icon: Users,
    title: uebersetzbar("Verein und Trainingsgruppen"),
    text: uebersetzbar("Vereine führen Mitglieder und Gruppen, Trainer planen Gruppentrainings und weisen Übungen zu. Mehrere Trainer können sich eine Gruppe teilen."),
  },
  {
    icon: WifiOff,
    title: uebersetzbar("Läuft auch ohne Netz"),
    text: uebersetzbar("Auf dem Feld gibt es selten guten Empfang. Eingaben werden lokal gespeichert und nachgereicht, sobald wieder Verbindung besteht."),
  },
  {
    icon: Dog,
    title: uebersetzbar("Mehrere Hunde, eine Historie"),
    text: uebersetzbar("Jeder Hund hat sein eigenes Tagebuch, seine Ziele und seine Auswertung - über Jahre hinweg nachvollziehbar."),
  },
];

export const FAQ = [
  {
    question: uebersetzbar("Was kostet Dogity?"),
    answer:
      uebersetzbar("Dogity ist kostenlos nutzbar. Es gibt kein Abonnement, keine Bezahlschranke und keine Werbung."),
  },
  {
    question: uebersetzbar("Wie kann ich Dogity unterstützen?"),
    answer:
      uebersetzbar("Freiwillig über Ko-fi. Dogity wird in der Freizeit entwickelt und aus eigener Tasche betrieben; jede Unterstützung hilft, Server und Domain zu bezahlen. Am Funktionsumfang ändert sie nichts - es gibt keine bezahlten Zusatzfunktionen."),
  },
  {
    question: uebersetzbar("Für welche Hundesportarten ist Dogity gedacht?"),
    answer:
      uebersetzbar("Für den Gebrauchshundesport nach VDH und FCI: Begleithundeprüfung (BH), IBGH 1 bis 3 und IGP 1 bis 3 mit Fährte, Unterordnung und Schutzdienst, dazu die Einzelprüfungen FPr, GPr, SPr, StöPr und UPr."),
  },
  {
    question: uebersetzbar("Brauche ich eine App aus dem App Store?"),
    answer:
      uebersetzbar("Nein. Dogity läuft im Browser und lässt sich auf dem Smartphone über 'Zum Home-Bildschirm hinzufügen' wie eine App ablegen - inklusive Offline-Betrieb."),
  },
  {
    question: uebersetzbar("Kann ich Fährten wirklich mit dem Handy aufzeichnen?"),
    answer:
      uebersetzbar("Ja. Beim Legen zeichnet das Handy die Fährte samt Winkeln und Gegenständen auf, beim Ablauf den Weg des Hundes. Beides wird übereinandergelegt und ausgewertet."),
  },
  {
    question: uebersetzbar("Kann unser Verein Dogity nutzen?"),
    answer:
      uebersetzbar("Ja. Vereine können Mitglieder und Trainingsgruppen verwalten, Trainer planen Gruppentrainings und geben den Mitgliedern Rückmeldung zu ihren Trainingseinheiten."),
  },
];
