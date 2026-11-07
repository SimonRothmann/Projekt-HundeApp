"use client";

import { useT } from "@/lib/i18n";
import { usePreferences } from "@/lib/preferences-context";
import { schritteText, wirksameSchrittlaenge } from "@/lib/schritte";

/**
 * "≈ 410 Schritte" zu einer Länge in Metern, mit der Schrittlänge aus den Einstellungen - null, wenn es
 * nichts anzuzeigen gibt. Nur für Fährten: Der Trail rechnet nicht in Schritten.
 */
export function useSchritte(): (meter: number | null | undefined) => string | null {
  const t = useT();
  const { preferences } = usePreferences();
  const schrittlaengeCm = wirksameSchrittlaenge(preferences.stepLengthCm);
  return (meter) => schritteText(meter, schrittlaengeCm, t);
}
