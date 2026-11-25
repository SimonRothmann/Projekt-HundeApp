"use client";

import { useEffect, useState } from "react";
import { useTheme } from "next-themes";
import { Building2, Database, GraduationCap, Palette, SlidersHorizontal, Sparkles, Trophy, Dumbbell } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useProfil } from "@/lib/use-profil";
import { usePreferences } from "@/lib/preferences-context";
import { useNeuerungenUngelesen } from "@/lib/neuerungen-gelesen";
import { AKTUELLE_VERSION } from "@/lib/aktuelle-version";
import { bestimmeSchriftgroesse } from "@/lib/schriftgroesse";
import { useSprache, useT } from "@/lib/i18n";
import { vorschauDarstellung, vorschauFunktionen, vorschauSportarten } from "@/lib/profil";
import { MODULE, type Sport } from "@/lib/types";
import { InstagramSymbol, INSTAGRAM_URL } from "@/components/ui/instagram-symbol";
import { ListenGruppe, ListenZeile } from "@/components/profile/einstellungs-liste";
import { ProfilKopf } from "@/components/profile/profil-kopf";
import { AbmeldenZeile } from "@/components/profile/abmelden-zeile";
import { SupportButton } from "@/components/support-button";
import { RechtlicheLinks } from "@/components/rechtliche-links";
import { ladeSportarten } from "@/lib/sportarten";

/**
 * Das Profil als Einstellungsliste (wie die iOS-Einstellungen): Jede Zeile
 * führt auf eine eigene Unterseite oder zeigt rechts, was dort gerade
 * eingestellt ist. Die Seite selbst passt auf einen Bildschirm; alles, was
 * früher hier ausgebreitet stand, liegt unter /profile/...
 */
export default function ProfilePage() {
  const t = useT();
  const sprache = useSprache();
  const { isTrainer } = useAuth();
  const { profil } = useProfil();
  const { preferences, moduleEnabled } = usePreferences();
  const { theme } = useTheme();
  const neuerungenUngelesen = useNeuerungenUngelesen();
  const [sportarten, setSportarten] = useState<Sport[] | null>(null);

  useEffect(() => {
    // Nur für die Vorschau "BH, Fährte"; fällt der Abruf aus, fehlt die Vorschau.
    ladeSportarten()
      .then(setSportarten)
      .catch(() => setSportarten(null));
  }, []);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Profil")}</h1>

      <ProfilKopf avatarUrl={profil?.avatarUrl ?? null} istGast={!!profil?.guestUntil} />

      {/* Verein und Sportarten: aus der unteren Leiste hierher gezogen. Beides
          öffnet man selten; die Leiste bleibt so bei höchstens fünf Zielen.
          Wer noch keinem Verein angehört, sieht die Beitrittskarte weiterhin
          auf der Startseite. */}
      <ListenGruppe>
        <ListenZeile href="/clubs" icon={Building2} titel={t("Mein Verein")} untertitel={t("Vereine finden, beitreten, gründen und verwalten")} />
        <ListenZeile href="/sports" icon={Trophy} titel={t("Sportarten")} untertitel={t("Prüfungsordnungen & Übungen entdecken")} />
        {/* Die Startseite zeigt die Zeile nur, solange keine BH-Karte den Link
            trägt; im Profil steht sie immer, wenn das Modul an ist - hier
            sucht man sie, wenn die Startseite sie nicht zeigt. */}
        {moduleEnabled(MODULE.sachkunde) && (
          <ListenZeile href="/lernen" icon={GraduationCap} titel={t("Sachkunde üben")} untertitel={t("Sachkunde üben wie beim Führerschein")} />
        )}
        <ListenZeile
          href="/profile/sportarten"
          icon={Dumbbell}
          titel={t("Sportarten, die ich trainiere")}
          // Die Schrittlänge liegt auf derselben Seite; ohne Hinweis suchte sie hier niemand.
          untertitel={moduleEnabled(MODULE.faehrte) ? t("Auch Schrittlänge für die Fährte") : undefined}
          vorschau={vorschauSportarten(preferences.sportIds, sportarten, t, sprache)}
        />
      </ListenGruppe>

      <ListenGruppe>
        <ListenZeile
          href="/profile/darstellung"
          icon={Palette}
          titel={t("Darstellung & Sprache")}
          vorschau={vorschauDarstellung(theme, bestimmeSchriftgroesse(preferences.fontScale), sprache, t)}
        />
        <ListenZeile
          href="/profile/funktionen"
          icon={SlidersHorizontal}
          titel={t("Funktionen")}
          vorschau={vorschauFunktionen(preferences.disabledModules, isTrainer, t)}
        />
      </ListenGruppe>

      {/* Angemeldete Nutzer sehen die Fußzeile der öffentlichen Seiten nie -
          ohne diese Zeile gäbe es für sie keinen Weg zu den Neuerungen. */}
      <ListenGruppe>
        <ListenZeile href="/profile/daten" icon={Database} titel={t("Daten & Konto")} />
        <ListenZeile
          href="/profile/neuerungen"
          icon={Sparkles}
          titel={t("Neu in Dogity")}
          vorschau={t("Version {v}", { v: AKTUELLE_VERSION })}
          punkt={neuerungenUngelesen}
          punktText={t("Neu")}
        />
        <ListenZeile
          href={INSTAGRAM_URL}
          extern
          icon={InstagramSymbol}
          titel={t("Dogity auf Instagram")}
          untertitel={t("Tipps, Neuerungen und Ayla")}
        />
      </ListenGruppe>

      <div className="flex flex-col items-center gap-2 pt-2 text-center">
        <p className="text-xs text-muted-foreground">{t("Gefällt dir Dogity? Über Unterstützung freue ich mich sehr.")}</p>
        <SupportButton />
      </div>

      {/* Auch im eingeloggten Bereich erreichbar - die Fußzeile der
          öffentlichen Seiten sieht hier niemand. */}
      <RechtlicheLinks />

      <AbmeldenZeile />
    </div>
  );
}
