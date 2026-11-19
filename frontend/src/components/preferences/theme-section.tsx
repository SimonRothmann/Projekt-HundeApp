"use client";

import { useTheme } from "next-themes";
import { Moon, SunMoon, Sun } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Chip } from "@/components/ui/chip";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Hell, dunkel oder wie das Gerät - derselbe Mechanismus wie der Umschalter in
 * der Kopfzeile (next-themes), nur mit beschrifteter Auswahl statt eines
 * Symbols. Vorgabe ist "Automatisch", also die Einstellung des Geräts (Apple
 * HIG Dark Mode, siehe ThemeProvider).
 *
 * Die Wahl liegt im Browser dieses Geräts, nicht im Konto: Ob ein Bildschirm
 * im Hellen oder im Dunkeln benutzt wird, ist eine Eigenschaft des Geräts.
 */
const STUFEN = [
  { key: "system", label: uebersetzbar("Automatisch"), icon: SunMoon },
  { key: "light", label: uebersetzbar("Hell"), icon: Sun },
  { key: "dark", label: uebersetzbar("Dunkel"), icon: Moon },
] as const;

export function ThemeSection() {
  const t = useT();
  const { theme, setTheme } = useTheme();

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("Erscheinungsbild")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-1.5">
        {STUFEN.map(({ key, label, icon: Icon }) => {
          const gewaehlt = (theme ?? "system") === key;
          return (
            <Chip key={key} gewaehlt={gewaehlt} onClick={() => setTheme(key)}>
              <Icon className="size-4" aria-hidden />
              {t(label)}
            </Chip>
          );
        })}
      </CardContent>
    </Card>
  );
}
