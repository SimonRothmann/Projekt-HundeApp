type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

// Ohne Übersetzer (Tests, Server) bleibt es bei den deutschen Sätzen.
const DEUTSCH: Uebersetzer = (text, werte) =>
  text.replace(/\{(\w+)\}/g, (treffer, name: string) => (werte && name in werte ? String(werte[name]) : treffer));

/**
 * Ein Fährten- oder Trailalter in Minuten als Text: "45 Min.", ab einer Stunde
 * "1 Std. 7 Min.", bei glatten Stunden "3 Std.". 1307 Minuten liest niemand.
 */
export function alterMinutenText(minuten: number, t: Uebersetzer = DEUTSCH): string {
  if (minuten < 60) return t("{n} Min.", { n: minuten });
  const std = Math.floor(minuten / 60);
  const rest = minuten % 60;
  return rest === 0 ? t("{n} Std.", { n: std }) : t("{std} Std. {min} Min.", { std, min: rest });
}
