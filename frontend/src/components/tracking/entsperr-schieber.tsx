"use client";

import { useRef, useState } from "react";
import { ChevronRight, Lock } from "lucide-react";
import { entsperrFortschritt, entsperrt } from "@/lib/aufnahme-sperre";
import { useT } from "@/lib/i18n";

/**
 * Schieber zum Entsperren der Aufzeichnung: Der Griff links muss bewusst über
 * die Spur nach rechts gewischt werden. Ein Streifen in der Tasche tippt
 * höchstens den Griff an und bewegt ihn nicht weit genug.
 *
 * Tastatur ist kein Taschenrisiko: Dort entsperren Enter/Leertaste den
 * Griff (click mit detail 0).
 */
export function EntsperrSchieber({ onEntsperrt }: { onEntsperrt: () => void }) {
  const t = useT();
  const spurRef = useRef<HTMLDivElement>(null);
  const griffRef = useRef<HTMLButtonElement>(null);
  const start = useRef<{ x: number; id: number; spur: number; griff: number } | null>(null);
  const [fortschritt, setFortschritt] = useState(0);
  const [zieht, setZieht] = useState(false);
  // Der Weg des Griffs in Pixeln, beim Berühren gemessen (kein Ref im Render).
  const [weg, setWeg] = useState(0);

  function beginnen(e: React.PointerEvent<HTMLButtonElement>) {
    if (e.pointerType === "mouse" && e.button !== 0) return;
    const spur = spurRef.current;
    const griff = griffRef.current;
    if (!spur || !griff) return;
    griff.setPointerCapture(e.pointerId);
    start.current = { x: e.clientX, id: e.pointerId, spur: spur.clientWidth, griff: griff.offsetWidth };
    setWeg(Math.max(0, spur.clientWidth - griff.offsetWidth));
    setZieht(true);
  }

  function bewegen(e: React.PointerEvent) {
    const s = start.current;
    if (!s || s.id !== e.pointerId) return;
    setFortschritt(entsperrFortschritt(e.clientX - s.x, s.spur, s.griff));
  }

  function beenden(e: React.PointerEvent, abgebrochen: boolean) {
    const s = start.current;
    if (!s || s.id !== e.pointerId) return;
    start.current = null;
    setZieht(false);
    const ziel = abgebrochen ? 0 : entsperrFortschritt(e.clientX - s.x, s.spur, s.griff);
    // Davor loslassen: Griff springt zurück.
    setFortschritt(0);
    if (entsperrt(ziel)) onEntsperrt();
  }

  return (
    <div
      ref={spurRef}
      className="relative flex h-14 w-full touch-none items-center justify-center overflow-hidden rounded-full bg-muted text-sm font-medium text-muted-foreground select-none [-webkit-touch-callout:none]"
      onContextMenu={(e) => e.preventDefault()}
    >
      <span className="pl-14 pr-4">{t("Zum Entsperren wischen")}</span>
      <ChevronRight aria-hidden className="absolute right-4 size-5 opacity-60" />
      <button
        ref={griffRef}
        type="button"
        aria-label={t("Entsperren")}
        onPointerDown={beginnen}
        onPointerMove={bewegen}
        onPointerUp={(e) => beenden(e, false)}
        onPointerCancel={(e) => beenden(e, true)}
        onContextMenu={(e) => e.preventDefault()}
        onClick={(e) => {
          // detail 0: per Tastatur (Enter/Leertaste), nicht per Zeiger.
          if (e.detail === 0) onEntsperrt();
        }}
        className="absolute inset-y-1 left-1 flex aspect-square touch-none items-center justify-center rounded-full bg-primary text-primary-foreground outline-none select-none focus-visible:ring-3 focus-visible:ring-ring/85"
        style={{
          transform: `translateX(${fortschritt * weg}px)`,
          // Zurückspringen mit Übergang, beim Ziehen folgt der Griff dem Finger ohne Verzögerung.
          transition: zieht ? "none" : "transform 150ms ease-out",
        }}
      >
        <Lock aria-hidden className="size-5" />
      </button>
    </div>
  );
}
