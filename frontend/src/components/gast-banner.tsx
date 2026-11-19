"use client";

import { FlaskConical } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { gastUhrzeit } from "@/lib/gast";
import { useSprache, useT } from "@/lib/i18n";
import { useProfil } from "@/lib/use-profil";
import { Button } from "@/components/ui/button";

/**
 * Hinweis im Gastzugang ("App ausprobieren"): wann alles gelöscht wird, und der Weg zu einem echten Konto
 * (abmelden, dann registrieren - der Gast wird nicht übernommen). Echte Konten sehen nichts.
 */
export function GastBanner({ umgebung }: { umgebung?: string }) {
  const t = useT();
  const sprache = useSprache();
  const { logout } = useAuth();
  const { profil } = useProfil();

  if (!profil?.guestUntil) return null;

  return (
    <div className={umgebung}>
      <div
        role="status"
        className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-warnung/40 bg-warnung/10 px-3 py-2 text-sm text-warnung-text"
      >
        <FlaskConical className="size-4 shrink-0" aria-hidden />
        <p className="min-w-0 flex-1 break-words">
          {t("Demo - wird um {zeit} Uhr gelöscht", { zeit: gastUhrzeit(profil.guestUntil, sprache) })}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="coarse:min-h-11"
          onClick={() => {
            logout();
            // Harter Seitenwechsel: Der App-Rahmen schickt eine abgemeldete Person per router.replace nach
            // /login, und der gewönne gegen ein router.push("/register").
            // eslint-disable-next-line @next/next/no-location-assign-relative-destination
            window.location.assign("/register");
          }}
        >
          {t("Eigenes Konto anlegen")}
        </Button>
      </div>
    </div>
  );
}
