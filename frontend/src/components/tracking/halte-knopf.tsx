"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/** So lange muss man drücken. Kurz genug für eine Hand, lang genug gegen ein Streifen in der Tasche. */
const HALTEDAUER_MS = 1000;

/**
 * Ein Knopf, der erst nach einer Sekunde Gedrückthalten auslöst.
 *
 * Für das Beenden einer Aufzeichnung. Rückmeldung vom Fährtelegen: Beim
 * Herausziehen des Handys aus der Tasche war die Aufzeichnung plötzlich
 * vorbei. Der Bildschirm bleibt beim Aufzeichnen absichtlich an (sonst hält
 * iOS die Standortabfrage an), und der Abschlussknopf war der größte auf dem
 * Bildschirm - breit, unten, genau wo die Hand zugreift - und löste mit
 * einer einzigen Berührung aus.
 *
 * Eine Rückfrage per Dialog wäre die andere Möglichkeit gewesen. Sie kostet
 * aber einen zweiten gezielten Tipp mit der Hand, die gerade nicht die Leine
 * hält, und ein Dialog erscheint dort, wo die nächste zufällige Berührung
 * ihn bestätigen kann. Halten ist eine Bewegung, und ein kurzes Streifen
 * reicht dafür nicht.
 *
 * Über die Tastatur gibt es kein Halten; dort fragt stattdessen der Dialog.
 */
export function HalteKnopf({
  children,
  onAusgeloest,
  bestaetigungsfrage,
  className,
}: {
  children: React.ReactNode;
  onAusgeloest: () => void;
  /** Für die Bedienung per Tastatur, wo es kein Gedrückthalten gibt. */
  bestaetigungsfrage: string;
  className?: string;
}) {
  const t = useT();
  const [haelt, setHaelt] = useState(false);
  const zeitgeberRef = useRef<number | null>(null);
  // Immer die neueste Fassung aufrufen, nicht die vom Moment des Drückens:
  // Während der einen Sekunde Halten kommen noch GPS-Punkte hinzu, und die
  // alte Fassung würde ohne sie speichern.
  const ausloesenRef = useRef(onAusgeloest);
  useEffect(() => {
    ausloesenRef.current = onAusgeloest;
  });

  useEffect(
    () => () => {
      if (zeitgeberRef.current !== null) window.clearTimeout(zeitgeberRef.current);
    },
    [],
  );

  function beginnen(e: React.PointerEvent) {
    // Nur die Haupttaste bzw. ein Finger - kein Rechtsklick.
    if (e.pointerType === "mouse" && e.button !== 0) return;
    if (zeitgeberRef.current !== null) return;
    setHaelt(true);
    zeitgeberRef.current = window.setTimeout(() => {
      zeitgeberRef.current = null;
      setHaelt(false);
      // Spürbare Bestätigung, wo es geht (Android; iOS kennt vibrate nicht).
      navigator.vibrate?.(40);
      ausloesenRef.current();
    }, HALTEDAUER_MS);
  }

  function loslassen(zuFrueh: boolean) {
    if (zeitgeberRef.current === null) return;
    window.clearTimeout(zeitgeberRef.current);
    zeitgeberRef.current = null;
    setHaelt(false);
    // Eine Meldung, keine Sammlung: Wer mehrmals zu kurz tippt, sieht sie
    // einmal statt übereinandergestapelt.
    if (zuFrueh) toast(t("Zum Beenden gedrückt halten."), { id: "halte-knopf" });
  }

  return (
    <button
      type="button"
      onPointerDown={beginnen}
      onPointerUp={() => loslassen(true)}
      onPointerCancel={() => loslassen(false)}
      onPointerLeave={() => loslassen(false)}
      // Langes Drücken öffnet sonst auf Android das Kontextmenü und auf iOS
      // die Textauswahl - beides mitten im Halten.
      onContextMenu={(e) => e.preventDefault()}
      onClick={(e) => {
        // detail 0: ausgelöst per Tastatur (Enter/Leertaste), nicht per Zeiger.
        if (e.detail === 0 && window.confirm(bestaetigungsfrage)) onAusgeloest();
      }}
      className={cn(
        // Aussehen wie Button variant="destructive", Größe wie vorher der
        // Abschlussknopf (h-14, volle Breite).
        "relative flex h-14 w-full touch-none flex-col items-center justify-center overflow-hidden rounded-md bg-destructive/10 px-4 text-base font-medium text-destructive outline-none select-none [-webkit-touch-callout:none] focus-visible:ring-3 focus-visible:ring-destructive/20 dark:bg-destructive/20 dark:focus-visible:ring-destructive/40",
        className,
      )}
    >
      {/* Der Fortschritt füllt den Knopf von links: Man sieht, dass Halten
          wirkt und wie lange noch. Zurück springt er ohne Übergang. */}
      <span
        aria-hidden
        className="absolute inset-y-0 left-0 bg-destructive/25 ease-linear"
        style={{
          width: haelt ? "100%" : "0%",
          transitionProperty: "width",
          transitionDuration: haelt ? `${HALTEDAUER_MS}ms` : "0ms",
        }}
      />
      <span className="relative">{children}</span>
      <span className="relative text-xs font-normal opacity-80">{t("gedrückt halten")}</span>
    </button>
  );
}
