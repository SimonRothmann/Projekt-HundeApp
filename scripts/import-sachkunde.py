#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Wandelt die swhv-Fragenkataloge zur BH/VT-Sachkundeprüfung in die
Seed-Datei des Backends um.

    python3 scripts/import-sachkunde.py ~/Downloads

Erwartet in dem übergebenen Ordner die vier PDFs des swhv:

    BH_Fragenkatalog_fuer_Erwachsene_Loesungen_neu.pdf
    BH_Fragenkatalog_fuer_Jugendliche_Loesungen.pdf

(die Fassungen ohne Lösungen werden nicht gebraucht - sie sind bis auf die
Ankreuzmarkierungen zeichengleich mit den Lösungsfassungen).

Schreibt:
    backend/src/Dogity.Infrastructure/Persistence/Seed/Data/sachkunde-swhv.json
    frontend/public/sachkunde/a2.jpg   (Bild zur Zuordnungsfrage A2)

Warum ein Skript und keine Handarbeit: der swhv aktualisiert seine Kataloge.
Erscheint eine neue Fassung, wird dieses Skript erneut ausgeführt und der
"stand" unten hochgesetzt - der Seeder gleicht dann anhand der Fragennummer ab
und ändert nur, was sich geändert hat.

Voraussetzung: poppler (pdftotext, pdfimages), z.B. `brew install poppler`.
"""
from __future__ import annotations

import json
import re
import shutil
import subprocess
import sys
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ZIEL_JSON = REPO / "backend/src/Dogity.Infrastructure/Persistence/Seed/Data/sachkunde-swhv.json"
ZIEL_BILDER = REPO / "frontend/public/sachkunde"

HERAUSGEBER = "Südwestdeutscher Hundesportverband e.V. (swhv)"
QUELLE = "https://swhv.de/fileadmin/swhv.de/Dokumente/Formulare_und_Texte/Basis/"
STAND = "2024-03"

# Themenkomplexe des Erwachsenenkatalogs. Die Überschriften im PDF nennen nur
# den Buchstaben; die Bezeichnungen sind aus den Fragen des jeweiligen Komplexes
# abgeleitet und dienen der Navigation, nicht der Prüfung.
KOMPLEXE = {
    "A": "Verhalten und Umgang mit dem Hund",
    "B": "Zucht, Aufzucht und Gesundheit",
    "C": "Recht",
    "D": "Kynologie, Verbände und Ausbildung",
    "E": "Prüfungswesen",
    "J": "Jugendfragen",
}

# Zeilen, die nur Seitendeko sind. Bewusst am Zeilenanfang UND -ende verankert:
# ein zu gieriges Muster reißt sonst Antworttexte mit heraus (die Jugendfrage 1
# trägt das Wort "Lösungsbogen" rechts auf derselben Zeile).
NUR_DEKO = re.compile(
    r"^\s*(Seite \d+ von \d+"
    r"|www\.swhv\.de\S*"
    r"|Fragen ?/? ?A?n?t?w?o?r?t?e?n? ?zum Komplex [A-E]"
    r"|swhv Jugendfragekatalog"
    r"|zur BH/VT- ?Sachkundepr\S*.*"
    r"|Fragenkatalog (Jugend|Erwachsene).*"
    r"|Feb\. \d{4}.*)\s*$")

# Zuordnungsfragen: "Boxer   E   A. langhaarig" bzw. "a) Angst   4"
ZUORDNUNG_DREISPALTIG = re.compile(r"^\s*(\S.*?)\s{2,}([A-E])\s{2,}([A-E])\.\s*(.+?)\s*$")
# Zwei Muster, weil die Spaltenbreite im PDF nicht durchgehalten wird: bei der
# durchbuchstabierten Liste steht vor der Lösung teils nur EIN Leerzeichen
# ("c) Aufforderung zum Spiel 3"), dort trägt das "a)" die Struktur. Ohne
# Buchstabe braucht es dagegen den Spaltenabstand, sonst wird jede zweite
# Fließtextzeile als Zuordnung gelesen.
ZUORDNUNG_GELISTET = re.compile(r"^\s*([a-e])\)\s*(\S.*?)\s+([A-E]|\d+)\s*$")
ZUORDNUNG_ZWEISPALTIG = re.compile(r"^\s*(\S.*?)\s{2,}([A-E]|\d+)\s*$")
NUR_OPTION = re.compile(r"^\s*([A-E])\.\s*(.+?)\s*$")

# pdftotext gibt die Ankreuzkästchen der Jugendfassung als Steuerzeichen aus
# (U+0088 für das leere, ":" für das angekreuzte). Das leere Kästchen blieb in
# der ersten Fassung in JEDEM Antworttext stehen - unsichtbar im Terminal,
# sichtbar in der App. Deshalb fliegen hier alle Steuerzeichen raus.
STEUERZEICHEN = re.compile(r"[\x00-\x1f\x7f-\x9f]")


def saubern(text: str) -> str:
    """Steuerzeichen entfernen und Leerraum vereinheitlichen."""
    return re.sub(r"\s+", " ", STEUERZEICHEN.sub("", text)).strip()


def pdftotext(pdf: Path) -> list[str]:
    if not pdf.exists():
        sys.exit(f"Nicht gefunden: {pdf}")
    roh = subprocess.run(["pdftotext", "-layout", str(pdf), "-"],
                         capture_output=True, text=True, check=True)
    return roh.stdout.splitlines()


def ist_deko(zeile: str) -> bool:
    return bool(NUR_DEKO.match(zeile))


def neue_frage(katalog: str, komplex: str, nummer: str, text: str, reihenfolge: int) -> dict:
    return {"katalog": katalog, "komplex": komplex, "nummer": nummer, "reihenfolge": reihenfolge,
            "text": text.strip(), "antworten": [], "paare": [], "optionen": [],
            "bild": None, "musterloesung": None}


def parse_erwachsene(zeilen: list[str]) -> list[dict]:
    fragen: list[dict] = []
    aktuell: dict | None = None
    komplex = "A"

    for zeile in zeilen:
        if treffer := re.search(r"zum Komplex ([A-E])\s*$", zeile):
            komplex = treffer.group(1)
            continue
        if ist_deko(zeile):
            continue

        # Fragennummer: "A 1:", "A1:", und - so steht es wirklich im PDF - "C. 3:"
        if treffer := re.match(r"\s*([A-E])\.? ?(\d+) ?:\.?\s*(.*)", zeile):
            if aktuell:
                fragen.append(aktuell)
            aktuell = neue_frage("erwachsene", komplex, f"{treffer.group(1)}{treffer.group(2)}",
                                 treffer.group(3), len(fragen) + 1)
            continue

        if aktuell is None:
            continue

        # Ankreuzzeile: "x ..." richtig, "□ ..." falsch
        if treffer := re.match(r"\s*([x☐□]) ?(.*)", zeile):
            aktuell["antworten"].append({"text": treffer.group(2).strip(),
                                         "richtig": treffer.group(1) == "x"})
            continue

        if treffer := ZUORDNUNG_DREISPALTIG.match(zeile):
            aktuell["paare"].append({"marke": None, "begriff": treffer.group(1).strip(),
                                     "loesung": treffer.group(2)})
            aktuell["optionen"].append({"schluessel": treffer.group(3), "text": treffer.group(4).strip()})
            continue

        if treffer := ZUORDNUNG_GELISTET.match(zeile):
            aktuell["paare"].append({"marke": treffer.group(1), "begriff": treffer.group(2).strip(),
                                     "loesung": treffer.group(3)})
            continue

        if treffer := NUR_OPTION.match(zeile):
            aktuell["optionen"].append({"schluessel": treffer.group(1), "text": treffer.group(2).strip()})
            continue

        if treffer := ZUORDNUNG_ZWEISPALTIG.match(zeile):
            aktuell["paare"].append({"marke": None, "begriff": treffer.group(1).strip(),
                                     "loesung": treffer.group(2)})
            continue

        if not zeile.strip():
            continue

        # Fortsetzungszeilen: gehören zur letzten Antwort, sonst zur Frage -
        # und nach einer Frage ganz ohne Ankreuzfelder ist es die Musterlösung.
        if aktuell["antworten"]:
            aktuell["antworten"][-1]["text"] += " " + zeile.strip()
        elif aktuell["paare"]:
            continue
        elif aktuell["text"].rstrip().endswith(("?", ":")):
            vorher = aktuell["musterloesung"]
            aktuell["musterloesung"] = (vorher + " " if vorher else "") + zeile.strip()
        else:
            aktuell["text"] += " " + zeile.strip()

    if aktuell:
        fragen.append(aktuell)
    return fragen


def parse_jugend(zeilen: list[str]) -> list[dict]:
    fragen: list[dict] = []
    aktuell: dict | None = None

    for zeile in zeilen:
        if ist_deko(zeile):
            continue
        if treffer := re.match(r"\s*(\d+)\.\s+(\D.*)", zeile):
            if aktuell:
                fragen.append(aktuell)
            aktuell = neue_frage("jugend", "J", treffer.group(1), treffer.group(2), len(fragen) + 1)
            continue
        if aktuell is None or not zeile.strip():
            continue
        # Im Jugendkatalog markiert das angekreuzte Kästchen die richtige
        # Antwort; pdftotext gibt es als ":" aus.
        richtig = bool(re.match(r"\s*[:\x88]*\s*:", zeile))
        text = saubern(re.sub(r"^\s*:?\s*", "", zeile))
        text = re.sub(r"\s+Lösungsbogen\s*$", "", text).strip()
        if text:
            aktuell["antworten"].append({"text": text, "richtig": richtig, "bild": None})

    if aktuell:
        fragen.append(aktuell)

    for frage in fragen:
        frage["antworten"] = ohne_layoutmuell(frage["antworten"])
    return fragen


def ohne_layoutmuell(antworten: list[dict]) -> list[dict]:
    """Entfernt, was aus dem Seitenlayout stammt statt aus der Frage.

    Die Bildfrage 30 ("Welcher Hund zeigt eine Spielhaltung?") hat drei
    Zeichnungen mit den Unterschriften 1, 2 und 3. pdftotext liest die
    Unterschriften als weitere Zeilen mit - einmal als "1  3" (die beiden
    äußeren stehen auf einer Höhe) und einmal als "2". Beides landete als
    zusätzliche Antwort in der App.

    Regel: eine reine Zahlenzeile ist nur dann eine Antwort, wenn diese Zahl
    nicht schon als Antwort dasteht.
    """
    ergebnis: list[dict] = []
    gesehen: set[str] = set()
    for antwort in antworten:
        text = antwort["text"]
        zahlen = text.split()
        if all(z.isdigit() for z in zahlen) and any(z in gesehen for z in zahlen):
            continue
        if text in gesehen:
            continue
        gesehen.add(text)
        ergebnis.append(antwort)
    return ergebnis


def art(frage: dict) -> str:
    if frage["paare"]:
        return "Assignment"
    if frage["musterloesung"]:
        return "FreeText"
    if sum(a["richtig"] for a in frage["antworten"]) > 1:
        return "MultipleChoice"
    return "SingleChoice"


def zuordnung_aufbereiten(frage: dict) -> dict:
    """Die Zuordnung als Aufgabe, nicht als Lösungssatz.

    Der erste Anlauf hat aus den Paaren nur einen Text gebaut ("Boxer -> E ...").
    Damit stand in der App eine Frage, die zum Zuordnen auffordert, ohne dass
    etwas zum Zuordnen da war - man konnte nur die Lösung aufdecken. Hier
    bleiben Begriffe und Beschriftungen getrennt erhalten.

    Die Auswahlmöglichkeiten leitet die Oberfläche aus den Schlüsseln ab. Das
    verrät nichts: die Zuordnung ist eineindeutig, jeder Schlüssel kommt genau
    einmal vor - gesucht ist die Reihenfolge, nicht die Menge.
    """
    beschriftung = {o["schluessel"]: o["text"] for o in frage["optionen"]}
    return {
        "begriffe": [
            {"text": p["begriff"], "schluessel": p["loesung"], "reihenfolge": i + 1}
            for i, p in enumerate(frage["paare"])
        ],
        "schluessel": [
            {"schluessel": s, "text": beschriftung[s], "reihenfolge": i + 1}
            for i, s in enumerate(sorted(beschriftung))
        ],
    }


def musterloesung_aus_paaren(frage: dict) -> str:
    # Ist die Zuordnung durchbuchstabiert, muss die Folge lückenlos sein. Genau
    # hier fiel eine Zeile durch: "c) Aufforderung zum Spiel 3" steht mit nur
    # einem Leerzeichen im PDF und wurde von einem zu strengen Muster
    # übersehen - die Lösung war dann still unvollständig.
    marken = [p["marke"] for p in frage["paare"] if p["marke"]]
    if marken and marken != [chr(ord("a") + i) for i in range(len(marken))]:
        sys.exit(f"{frage['nummer']}: Zuordnung hat eine Lücke - erkannt: {marken}")

    beschriftung = {o["schluessel"]: o["text"] for o in frage["optionen"]}
    return " · ".join(
        f"{p['begriff']} → {p['loesung']}" + (f" ({beschriftung[p['loesung']]})" if p["loesung"] in beschriftung else "")
        for p in frage["paare"])


def aufbereiten(frage: dict) -> dict:
    kind = art(frage)
    ergebnis = {
        "nummer": frage["nummer"],
        "komplex": frage["komplex"],
        "reihenfolge": frage["reihenfolge"],
        "art": kind,
        "text": saubern(frage["text"]),
        "bild": frage["bild"],
        "musterloesung": None,
        "antworten": [],
        "zuordnung": None,
    }
    if kind == "Assignment":
        ergebnis["musterloesung"] = musterloesung_aus_paaren(frage)
        ergebnis["zuordnung"] = zuordnung_aufbereiten(frage)
    elif kind == "FreeText":
        ergebnis["musterloesung"] = saubern(frage["musterloesung"])
    else:
        ergebnis["antworten"] = [
            {"text": saubern(a["text"]), "richtig": a["richtig"], "reihenfolge": i + 1,
             "bild": a.get("bild")}
            for i, a in enumerate(frage["antworten"])]
    return ergebnis


def pruefen(name: str, fragen: list[dict]) -> None:
    """Bricht ab, wenn der Katalog nicht plausibel ist - eine halb geparste
    Seed-Datei fällt sonst erst beim Start der Instanz auf."""
    fehler = []
    for f in fragen:
        if not f["text"]:
            fehler.append(f"{f['nummer']}: keine Fragestellung")
        if f["art"] in ("SingleChoice", "MultipleChoice"):
            if len(f["antworten"]) < 2:
                fehler.append(f"{f['nummer']}: weniger als zwei Antworten")
            if not any(a["richtig"] for a in f["antworten"]):
                fehler.append(f"{f['nummer']}: keine richtige Antwort markiert")
        elif not f["musterloesung"]:
            fehler.append(f"{f['nummer']}: {f['art']} ohne Musterlösung")
        if f["art"] == "Assignment":
            begriffe = (f["zuordnung"] or {}).get("begriffe") or []
            if len(begriffe) < 2:
                fehler.append(f"{f['nummer']}: Zuordnung mit weniger als zwei Begriffen")
            schluessel = [b["schluessel"] for b in begriffe]
            if len(set(schluessel)) != len(schluessel):
                fehler.append(f"{f['nummer']}: Zuordnung nutzt einen Schlüssel doppelt")
    nummern = [f["nummer"] for f in fragen]
    for doppelt, anzahl in Counter(nummern).items():
        if anzahl > 1:
            fehler.append(f"{doppelt}: {anzahl}-mal vorhanden")
    if fehler:
        sys.exit(f"{name}: Katalog nicht plausibel -\n  " + "\n  ".join(fehler))


def groesste_bilder(pdf: Path, seite: int, anzahl: int, endung: str) -> list[Path]:
    """Die N größten Bilder einer Seite, in der Reihenfolge, in der das PDF sie führt.

    Auf einer Seite stecken neben den Zeichnungen dutzende Winzbilder (Reste der
    Schriftdarstellung, wenige hundert Byte). Nach Größe zu sortieren trennt die
    Zeichnungen sauber ab; die Rückgabe steht danach wieder in Objektreihenfolge,
    weil die Zuordnung darauf aufbaut.
    """
    tmp = ZIEL_BILDER / "_extract"
    schalter = "-j" if endung == "jpg" else "-png"
    subprocess.run(["pdfimages", "-f", str(seite), "-l", str(seite), schalter, str(pdf), str(tmp)], check=True)
    alle = sorted(ZIEL_BILDER.glob(f"_extract-*.{endung}"))
    grosse = sorted(sorted(alle, key=lambda p: p.stat().st_size, reverse=True)[:anzahl])
    for rest in ZIEL_BILDER.glob("_extract-*"):
        if rest not in grosse:
            rest.unlink()
    return grosse


def bilder_holen(erw_pdf: Path, jgd_pdf: Path, erwachsene: list[dict], jugend: list[dict]) -> None:
    """Zeichnungen aus beiden PDFs übernehmen.

    Zwei Fragen sind ohne Abbildung sinnlos:

    A2 (Erwachsene) zeigt fünf Körperhaltungen in EINER Zeichnung, die
    Zuordnung verweist auf die Ziffern darin - ein Bild an der Frage.

    Jugend 30 ("Welcher Hund zeigt eine Spielhaltung?") zeigt drei einzelne
    Zeichnungen, je eine je Antwort - drei Bilder an den Antworten.
    """
    for bild in groesste_bilder(erw_pdf, seite=1, anzahl=1, endung="jpg"):
        shutil.move(str(bild), ZIEL_BILDER / "a2.jpg")
    for frage in erwachsene:
        if frage["nummer"] == "A2":
            frage["bild"] = "a2.jpg"

    zeichnungen = groesste_bilder(jgd_pdf, seite=5, anzahl=3, endung="png")
    if len(zeichnungen) != 3:
        return

    # Die Reihenfolge der eingebetteten Bilder ist NICHT die Reihenfolge auf der
    # Seite. Gegen die gerenderte Seite 5 geprüft: die Unterschrift 1 gehört zur
    # zweiten eingebetteten Zeichnung, die 2 zur ersten, die 3 zur dritten.
    #
    # In die Zeichnungen selbst sind noch die Zahlen 3, 4 und 2 eingebrannt -
    # Reste derselben Vorlage, aus der auch A2 stammt. Maßgeblich sind die
    # Unterschriften des PDF, nicht die eingebrannten Zahlen.
    UNTERSCHRIFT_ZU_BILD = {"1": 1, "2": 0, "3": 2}

    for frage in jugend:
        if frage["nummer"] != "30":
            continue
        for antwort in frage["antworten"]:
            index = UNTERSCHRIFT_ZU_BILD.get(antwort["text"].strip())
            if index is None:
                continue
            ziel = ZIEL_BILDER / f"jgd30-{antwort['text'].strip()}.png"
            shutil.copy(str(zeichnungen[index]), ziel)
            antwort["bild"] = ziel.name

    for rest in zeichnungen:
        rest.unlink(missing_ok=True)


def main() -> None:
    quelle = Path(sys.argv[1] if len(sys.argv) > 1 else "~/Downloads").expanduser()

    erw_pdf = quelle / "BH_Fragenkatalog_fuer_Erwachsene_Loesungen_neu.pdf"
    jgd_pdf = quelle / "BH_Fragenkatalog_fuer_Jugendliche_Loesungen.pdf"

    erwachsene = [aufbereiten(f) for f in parse_erwachsene(pdftotext(erw_pdf))]
    jugend = [aufbereiten(f) for f in parse_jugend(pdftotext(jgd_pdf))]

    ZIEL_BILDER.mkdir(parents=True, exist_ok=True)
    bilder_holen(erw_pdf, jgd_pdf, erwachsene, jugend)

    pruefen("Erwachsene", erwachsene)
    pruefen("Jugend", jugend)

    daten = {
        "herausgeber": HERAUSGEBER,
        "quelle": QUELLE,
        "stand": STAND,
        "komplexe": KOMPLEXE,
        "kataloge": [
            {"code": "SWHV-BHVT-ERW", "name": "Sachkunde BH/VT – Erwachsene",
             "zielgruppe": "Adults",
             "beschreibung": "Fragenkatalog des swhv zur Sachkundeprüfung im Rahmen der "
                             "Begleithundeprüfung mit Verkehrsteil, Fassung für Erwachsene.",
             "fragen": erwachsene},
            {"code": "SWHV-BHVT-JGD", "name": "Sachkunde BH/VT – Jugend",
             "zielgruppe": "Youth",
             "beschreibung": "Fassung für Kinder und Jugendliche unter 15 Jahren. In der Prüfung "
                             "werden 15 Fragen gestellt, je Frage ist genau eine Antwort richtig.",
             "fragen": jugend},
        ],
    }

    ZIEL_JSON.parent.mkdir(parents=True, exist_ok=True)
    ZIEL_JSON.write_text(json.dumps(daten, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")

    for katalog in daten["kataloge"]:
        arten = Counter(f["art"] for f in katalog["fragen"])
        komplexe = Counter(f["komplex"] for f in katalog["fragen"])
        print(f"{katalog['code']}: {len(katalog['fragen'])} Fragen  "
              f"{dict(sorted(arten.items()))}  {dict(sorted(komplexe.items()))}")
    print(f"→ {ZIEL_JSON.relative_to(REPO)}")
    print(f"→ {ZIEL_BILDER.relative_to(REPO)}/")


if __name__ == "__main__":
    main()
