"use client";

import { useT } from "@/lib/i18n";

/**
 * Die Passwortregeln in einem Satz, unter dem Feld.
 *
 * Der Satz muss zu AddIdentity im Backend passen (mindestens 8 Zeichen,
 * Großbuchstabe, Kleinbuchstabe, Ziffer; Sonderzeichen nicht verlangt) - bei
 * einer Änderung dort ändert sich auch dieser Text. Vorher erfuhr man die
 * Regeln erst aus der Fehlermeldung nach dem Absenden. Das Feld verweist per
 * aria-describedby auf die id, damit auch Screenreader die Regeln beim Feld
 * hören.
 */
export function PasswortHinweis({ id }: { id: string }) {
  const t = useT();
  return (
    <p id={id} className="text-xs text-muted-foreground">
      {t("Mindestens 8 Zeichen, mit Groß- und Kleinbuchstabe und einer Ziffer.")}
    </p>
  );
}
