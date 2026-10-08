"use client";

import { useTheme } from "next-themes";
import { Moon, Sun } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Hell oder dunkel - derselbe Mechanismus wie der Umschalter in der
 * Kopfzeile (next-themes), nur mit beschrifteter Auswahl statt eines
 * Symbols. Dunkel bleibt die Vorgabe der App (siehe ThemeProvider).
 *
 * Die Wahl liegt im Browser dieses Geräts, nicht im Konto: Ob ein Bildschirm
 * im Hellen oder im Dunkeln benutzt wird, ist eine Eigenschaft des Geräts.
 */
const STUFEN = [
  { key: "light", label: uebersetzbar("Hell"), icon: Sun },
  { key: "dark", label: uebersetzbar("Dunkel"), icon: Moon },
] as const;

export function ThemeSection() {
  const t = useT();
  const { resolvedTheme, setTheme } = useTheme();

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("Erscheinungsbild")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-1.5">
        {STUFEN.map(({ key, label, icon: Icon }) => {
          const gewaehlt = (resolvedTheme ?? "dark") === key;
          return (
            <button
              key={key}
              type="button"
              aria-pressed={gewaehlt}
              onClick={() => setTheme(key)}
              className={cn(
                "inline-flex min-h-11 items-center gap-2 rounded-full border px-4 text-sm transition-colors",
                gewaehlt
                  ? "border-primary bg-primary/10 font-medium text-foreground"
                  : "border-input text-muted-foreground hover:border-foreground/30 hover:text-foreground",
              )}
            >
              <Icon className="size-4" aria-hidden />
              {t(label)}
            </button>
          );
        })}
      </CardContent>
    </Card>
  );
}
