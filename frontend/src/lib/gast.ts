import type { Sprache } from "@/lib/i18n";

/** Die Uhrzeit (HH:MM, Ortszeit des Geräts), zu der der Gastzugang gelöscht wird. */
export function gastUhrzeit(guestUntil: string, sprache: Sprache): string {
  return new Date(guestUntil).toLocaleTimeString(sprache === "en" ? "en-GB" : "de-DE", {
    hour: "2-digit",
    minute: "2-digit",
  });
}
