"use client";

import { useT } from "@/lib/i18n";

/** Titel und Einleitung der Sachkunde-Übersicht in der App - übersetzt, anders als die öffentliche Seite. */
export function LernenUeberschrift() {
  const t = useT();
  return (
    <div className="flex flex-col gap-2">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Sachkunde üben")}</h1>
      <p className="text-sm text-muted-foreground">
        {t("Der theoretische Teil der BH/VT: Frage für Frage, mit sofortiger Auflösung. Falsch beantwortete Fragen kommen wieder, bis sie sitzen.")}
      </p>
    </div>
  );
}
