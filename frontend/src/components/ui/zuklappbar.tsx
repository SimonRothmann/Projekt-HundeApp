"use client";

import { useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Der gemeinsame Weg, einen Bereich weich auf- und zuzuklappen: Die Höhe läuft über
 * `grid-template-rows` von 0fr nach 1fr (keine feste Höhe nötig, auch bei wechselndem Inhalt).
 * Aufgebaut wird der Inhalt erst beim ersten Öffnen; danach bleibt er im Baum, damit das Zuklappen
 * animieren kann. Zu ist er `inert` (kein Fokus, nicht für Screenreader). Bei
 * `prefers-reduced-motion` springt der Bereich ohne Bewegung auf und zu.
 *
 * `className` liegt auf dem Inhalt (z. B. `flex flex-col gap-2 pt-2`): Abstände gehören hinein, nicht
 * an die Eltern, sonst bliebe im zugeklappten Zustand eine Lücke.
 */
export function Zuklappbar({
  offen,
  id,
  className,
  children,
}: {
  offen: boolean;
  id?: string;
  className?: string;
  children: ReactNode;
}) {
  const [geoeffnet, setGeoeffnet] = useState(offen);
  if (offen && !geoeffnet) setGeoeffnet(true);
  if (!geoeffnet) return null;

  return (
    <div
      id={id}
      className={cn(
        "grid motion-safe:transition-[grid-template-rows] motion-safe:duration-200 motion-safe:ease-out",
        offen ? "grid-rows-[1fr] motion-safe:animate-aufklappen" : "grid-rows-[0fr]",
      )}
    >
      <div inert={!offen} className="min-h-0 overflow-hidden">
        <div className={className}>{children}</div>
      </div>
    </div>
  );
}
