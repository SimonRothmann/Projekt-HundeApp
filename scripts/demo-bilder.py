#!/usr/bin/env python3
"""Erzeugt die gezeichneten Hundefotos der Demo-Szenarien (nur Test-Umgebung).

Aufruf (Pillow nötig, `pip install pillow`):

    python3 scripts/demo-bilder.py

Schreibt fünf JPEG-Dateien nach
backend/src/Dogity.Infrastructure/Persistence/Seed/Data/DemoBilder/. Sie sind als
eingebettete Ressourcen im Backend und werden vom DemoSzenarienSeeder als
Hundefotos angelegt (Hundename = Dateiname, siehe docs/DEMO_DATEN.md).

Die Bilder sind selbst gezeichnet (einfache Hundemotive, keine Fotos aus dem Netz,
kein fremdes Urheberrecht) und absichtlich verschieden, damit sich die Darstellung
prüfen lässt:

  bello  quadratisch, DUNKLER Hintergrund, hellgelber Labrador
  emma   quadratisch, KRÄFTIGE Farbe (Orange), blau-grauer Australian Shepherd
  luna   QUERFORMAT 512x384, HELLER Hintergrund, liegender Border Collie
  rocky  HOCHFORMAT 384x512, sitzender Malinois vor blauem Himmel
  bella  quadratisch, grüne Wiese, flauschiger heller Hund

Quadratisch 512 x 512 ist das, was die App beim Hochladen erzeugt
(frontend/src/lib/image-resize.ts); Hoch- und Querformat kennt nur der Weg über
die Schnittstelle, die Hundeseite schneidet sie mit object-cover zu. Jedes Bild
bleibt unter 60 KB. Die Zeichnung ist deterministisch (feste Zufallszahlen):
gleiche Eingabe, gleiche Bytes.
"""

import io
import os
import random

from PIL import Image, ImageDraw, ImageFilter

SS = 3  # Überabtastung für glatte Kanten
ZIEL = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "backend", "src", "Dogity.Infrastructure", "Persistence", "Seed", "Data", "DemoBilder",
)
MAX_BYTES = 60 * 1024


def verlauf(breite, hoehe, oben, unten):
    bild = Image.new("RGB", (breite, hoehe))
    zeichner = ImageDraw.Draw(bild)
    for y in range(hoehe):
        t = y / max(1, hoehe - 1)
        farbe = tuple(round(oben[i] + (unten[i] - oben[i]) * t) for i in range(3))
        zeichner.line([(0, y), (breite, y)], fill=farbe)
    return bild


def ellipse(d, cx, cy, rx, ry, farbe):
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=farbe)


def auge(d, x, y, r):
    ellipse(d, x, y, r, r, (30, 22, 18))
    ellipse(d, x - r * 0.3, y - r * 0.35, r * 0.3, r * 0.3, (255, 255, 255))


def kopf(d, cx, cy, s, fell, ohr, schnauze, nase=(25, 20, 20), zunge=True, ohr_aufrecht=False, maske=None):
    """Hundekopf von vorn; s = Kopfradius."""
    if ohr_aufrecht:
        for seite in (-1, 1):
            d.polygon([(cx + seite * s * 0.95, cy - s * 0.35), (cx + seite * s * 0.55, cy - s * 1.45),
                       (cx + seite * s * 0.15, cy - s * 0.8)], fill=ohr)
    else:
        for seite in (-1, 1):
            ellipse(d, cx + seite * s * 0.95, cy + s * 0.1, s * 0.38, s * 0.8, ohr)
    ellipse(d, cx, cy, s, s * 0.92, fell)
    if maske is not None:
        ellipse(d, cx, cy + s * 0.38, s * 0.62, s * 0.5, maske)
    ellipse(d, cx, cy + s * 0.4, s * 0.55, s * 0.42, schnauze)
    ellipse(d, cx, cy + s * 0.22, s * 0.2, s * 0.13, nase)
    d.line([(cx, cy + s * 0.3), (cx, cy + s * 0.55)], fill=nase, width=max(1, round(s * 0.03)))
    d.arc([cx - s * 0.3, cy + s * 0.42, cx, cy + s * 0.72], 10, 170, fill=nase, width=max(1, round(s * 0.03)))
    d.arc([cx, cy + s * 0.42, cx + s * 0.3, cy + s * 0.72], 10, 170, fill=nase, width=max(1, round(s * 0.03)))
    if zunge:
        ellipse(d, cx, cy + s * 0.76, s * 0.14, s * 0.2, (232, 98, 120))
    auge(d, cx - s * 0.38, cy - s * 0.2, s * 0.085)
    auge(d, cx + s * 0.38, cy - s * 0.2, s * 0.085)


def rauschen(bild, staerke, saat):
    zufall = random.Random(saat)
    pixel = bild.load()
    for y in range(0, bild.height):
        for x in range(0, bild.width):
            if zufall.random() < 0.5:
                r, g, b = pixel[x, y]
                n = zufall.randint(-staerke, staerke)
                pixel[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)))
    return bild


def abschluss(bild, saat):
    """Kleine Unschärfe und Körnung, damit es eher nach Foto als nach Clip-Art aussieht."""
    bild = bild.filter(ImageFilter.GaussianBlur(0.6))
    return rauschen(bild, 5, saat)


def bello(b, h):
    # Dunkler Hintergrund, Labrador (gelb) im Brustbild.
    bild = verlauf(b * SS, h * SS, (22, 30, 44), (8, 12, 20))
    d = ImageDraw.Draw(bild)
    ellipse(d, 256 * SS, 560 * SS, 230 * SS, 190 * SS, (212, 160, 70))
    ellipse(d, 256 * SS, 470 * SS, 90 * SS, 60 * SS, (224, 176, 88))
    kopf(d, 256 * SS, 240 * SS, 130 * SS, (222, 176, 86), (186, 134, 52), (238, 200, 130))
    # Halsband
    d.rectangle([160 * SS, 400 * SS, 352 * SS, 424 * SS], fill=(200, 40, 50))
    ellipse(d, 256 * SS, 440 * SS, 16 * SS, 16 * SS, (235, 200, 70))
    return bild


def emma(b, h):
    # Kräftiger orangefarbener Hintergrund, blau-grauer Aussie mit weißer Blesse.
    bild = verlauf(b * SS, h * SS, (255, 120, 30), (230, 70, 10))
    d = ImageDraw.Draw(bild)
    ellipse(d, 256 * SS, 570 * SS, 230 * SS, 190 * SS, (98, 112, 134))
    ellipse(d, 256 * SS, 500 * SS, 80 * SS, 80 * SS, (245, 245, 245))
    kopf(d, 256 * SS, 250 * SS, 130 * SS, (104, 120, 144), (74, 86, 108), (248, 244, 238))
    # weiße Blesse und kupferfarbene Augenbrauen
    d.polygon([(256 * SS, 120 * SS), (232 * SS, 250 * SS), (280 * SS, 250 * SS)], fill=(248, 248, 248))
    for seite in (-1, 1):
        ellipse(d, (256 + seite * 98) * SS, 205 * SS, 14 * SS, 9 * SS, (196, 118, 64))
    return bild


def luna(b, h):
    # Querformat 512x384: heller Hintergrund, liegender Border Collie (schwarz-weiß).
    bild = verlauf(b * SS, h * SS, (250, 246, 236), (232, 224, 208))
    d = ImageDraw.Draw(bild)
    d.rectangle([0, 292 * SS, b * SS, h * SS], fill=(196, 208, 176))
    ellipse(d, 250 * SS, 306 * SS, 190 * SS, 52 * SS, (210, 205, 190))  # Schatten
    ellipse(d, 210 * SS, 262 * SS, 150 * SS, 60 * SS, (26, 26, 30))  # Körper
    ellipse(d, 130 * SS, 300 * SS, 70 * SS, 22 * SS, (245, 245, 245))  # weiße Pfoten
    ellipse(d, 330 * SS, 300 * SS, 70 * SS, 22 * SS, (245, 245, 245))
    # Rute
    d.polygon([(70 * SS, 250 * SS), (30 * SS, 290 * SS), (80 * SS, 290 * SS)], fill=(30, 30, 34))
    kopf(d, 372 * SS, 214 * SS, 70 * SS, (30, 30, 34), (20, 20, 24), (236, 232, 224), zunge=True)
    d.polygon([(372 * SS, 148 * SS), (352 * SS, 214 * SS), (392 * SS, 214 * SS)], fill=(248, 248, 248))
    return bild


def rocky(b, h):
    # Hochformat 384x512: sitzender Malinois vor Himmelblau.
    bild = verlauf(b * SS, h * SS, (120, 176, 230), (200, 224, 244))
    d = ImageDraw.Draw(bild)
    d.rectangle([0, 450 * SS, b * SS, h * SS], fill=(116, 150, 96))
    ellipse(d, 192 * SS, 400 * SS, 120 * SS, 130 * SS, (190, 140, 84))  # Hinterhand
    ellipse(d, 192 * SS, 340 * SS, 84 * SS, 130 * SS, (200, 150, 92))  # Brust
    for seite in (-1, 1):
        ellipse(d, (192 + seite * 38) * SS, 440 * SS, 20 * SS, 56 * SS, (186, 136, 80))  # Vorderbeine
    kopf(d, 192 * SS, 160 * SS, 82 * SS, (204, 154, 96), (36, 28, 24), (196, 146, 90),
         zunge=False, ohr_aufrecht=True, maske=(40, 32, 28))
    d.rectangle([118 * SS, 252 * SS, 266 * SS, 270 * SS], fill=(36, 36, 40))
    return bild


def bella(b, h):
    # Grüne Wiese, flauschiger heller Hund (Brustbild mit viel Fell).
    bild = verlauf(b * SS, h * SS, (150, 200, 160), (86, 150, 96))
    d = ImageDraw.Draw(bild)
    ellipse(d, 256 * SS, 570 * SS, 240 * SS, 200 * SS, (240, 214, 170))
    zufall = random.Random(7)
    for _ in range(140):  # Fellbüschel
        x = zufall.randint(40, 472)
        y = zufall.randint(400, 600)
        ellipse(d, x * SS, y * SS, zufall.randint(14, 26) * SS, zufall.randint(10, 20) * SS, (246, 224, 184))
    kopf(d, 256 * SS, 245 * SS, 125 * SS, (244, 220, 176), (222, 186, 130), (252, 238, 214))
    for seite in (-1, 1):
        ellipse(d, (256 + seite * 118) * SS, 170 * SS, 52 * SS, 36 * SS, (246, 224, 184))  # Stirnfell
    return bild


BILDER = [
    ("bello", 512, 512, bello, 11),
    ("emma", 512, 512, emma, 12),
    ("luna", 512, 384, luna, 13),
    ("rocky", 384, 512, rocky, 14),
    ("bella", 512, 512, bella, 15),
]


def main():
    os.makedirs(ZIEL, exist_ok=True)
    for name, b, h, zeichne, saat in BILDER:
        gross = zeichne(b, h)
        bild = gross.resize((b, h), Image.LANCZOS)
        bild = abschluss(bild, saat)
        for qualitaet in (85, 80, 75, 70, 65, 60):
            puffer = io.BytesIO()
            bild.save(puffer, "JPEG", quality=qualitaet, optimize=True)
            if puffer.tell() <= MAX_BYTES:
                break
        else:
            raise SystemExit(f"{name}: auch bei Qualität 60 über {MAX_BYTES} Bytes")
        pfad = os.path.join(ZIEL, f"{name}.jpg")
        with open(pfad, "wb") as datei:
            datei.write(puffer.getvalue())
        print(f"{name}.jpg {b}x{h}, {puffer.tell() / 1024:.1f} KB (Qualität {qualitaet})")


if __name__ == "__main__":
    main()
