"use client";

import { ToggleLeft } from "lucide-react";
import { useT } from "@/lib/i18n";
import { MODUL_TEXTE } from "@/lib/profil";
import type { AdminDisabledModule } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Balkenzeile } from "@/components/admin/nutzung-balken";

/** Wie viele Konten welches Modul in ihren Einstellungen abgeschaltet haben. */
export function NutzungModuleSection({ module, gesamt }: { module: AdminDisabledModule[]; gesamt: number }) {
  const t = useT();
  const titel = (key: string) => {
    const text = MODUL_TEXTE.find((m) => m.key === key)?.titel;
    return text ? t(text) : key;
  };
  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <ToggleLeft className="size-5 shrink-0" />
          {t("Abgeschaltete Module")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Wie viele Konten ein Modul in den Einstellungen abgeschaltet haben.")}
        </p>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-3">
          {module.map((m) => (
            <Balkenzeile key={m.key} label={titel(m.key)} wert={m.accounts} bezug={gesamt} />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
