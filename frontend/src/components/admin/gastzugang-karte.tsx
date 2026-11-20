"use client";

import { FlaskConical } from "lucide-react";
import { useT } from "@/lib/i18n";
import type { AdminGastzugang } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * "App ausprobieren" (Gastzugang): wie viele Gäste angelegt wurden und wie viele sich daraus
 * mit eigenem Konto registriert haben. Nur Zählungen - die Gäste selbst sind nach Stunden gelöscht
 * und zählen in keiner anderen Statistik mit.
 */
export function GastzugangKarte({ zahlen }: { zahlen: AdminGastzugang }) {
  const t = useT();
  const zeilen = [
    { label: t("Letzte 7 Tage"), angelegt: zahlen.created7, registriert: zahlen.registered7 },
    { label: t("Letzte 30 Tage"), angelegt: zahlen.created30, registriert: zahlen.registered30 },
  ];

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <FlaskConical className="size-5 shrink-0" />
          {t("App ausprobieren")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Gäste, die die App ohne Anmeldung ausprobiert haben, und daraus Registrierte (über den Knopf im Gast-Banner).")}
        </p>
      </CardHeader>
      <CardContent>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-muted-foreground">
              <th scope="col" className="pb-1 text-left font-normal">
                <span className="sr-only">{t("Zeitraum")}</span>
              </th>
              <th scope="col" className="pb-1 pl-3 text-right font-normal">
                {t("Gäste")}
              </th>
              <th scope="col" className="pb-1 pl-3 text-right font-normal">
                {t("davon registriert")}
              </th>
            </tr>
          </thead>
          <tbody>
            {zeilen.map((z) => (
              <tr key={z.label}>
                <th scope="row" className="py-1 text-left font-medium">
                  {z.label}
                </th>
                <td className="py-1 pl-3 text-right text-lg font-semibold tabular-nums">{z.angelegt}</td>
                <td className="py-1 pl-3 text-right text-lg font-semibold tabular-nums">{z.registriert}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </CardContent>
    </Card>
  );
}
