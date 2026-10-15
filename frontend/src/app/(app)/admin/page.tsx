"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { BarChart3, Building2, ChevronRight, GraduationCap, Settings2, Trophy, Users } from "lucide-react";
import { api } from "@/lib/api";
import { resetUserZiel, zuErledigenZeilen } from "@/lib/admin";
import { useT } from "@/lib/i18n";
import type { AdminOrphanedData, AdminStats, Club, ClubRegistration } from "@/lib/types";
import { KennzahlenKarte } from "@/components/admin/kennzahlen-karte";
import { ListenGruppe, ListenZeile } from "@/components/profile/einstellungs-liste";

/**
 * Admin-Übersicht, gebaut wie das Profil: oben, was zu tun ist, dann die
 * Kennzahlen kurz, dann die Bereiche - jeder eine eigene Unterseite. Passt
 * auf einen Handy-Bildschirm.
 */
export default function AdminPage() {
  // useSearchParams braucht eine Suspense-Grenze, sonst fiele die ganze Seite
  // aus dem Prerendering.
  return (
    <Suspense fallback={null}>
      <AdminWeiche />
    </Suspense>
  );
}

/**
 * Alte Benachrichtigungen zeigen auf `/admin?resetUser=<id>` (Passwort-Reset
 * ohne Mailversand), solange der Adminbereich noch eine einzige Seite war.
 * Solche Links führen auf die Nutzerverwaltung weiter; ohne (gültiges)
 * ?resetUser erscheint die Übersicht.
 */
function AdminWeiche() {
  const t = useT();
  const router = useRouter();
  const ziel = resetUserZiel(useSearchParams().get("resetUser"));

  useEffect(() => {
    if (ziel) router.replace(ziel);
  }, [ziel, router]);

  if (ziel) return <p className="text-muted-foreground">{t("Lädt…")}</p>;
  return <Uebersicht />;
}

function Uebersicht() {
  const t = useT();
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [antraege, setAntraege] = useState<number | null>(null);
  const [verwaisteKonten, setVerwaisteKonten] = useState<number | null>(null);
  const [vereine, setVereine] = useState<number | null>(null);

  useEffect(() => {
    // Vier unabhängige Abrufe: Fällt einer aus, fehlt nur seine Angabe - die
    // Bereiche darunter bleiben bedienbar (kein Toast, die Seite ist nur die Tür).
    let aktuell = true;
    function laden<T>(pfad: string, uebernehmen: (daten: T) => void) {
      api
        .get<T>(pfad)
        .then((daten) => {
          if (aktuell) uebernehmen(daten);
        })
        .catch(() => {});
    }
    laden<AdminStats>("/api/admin/stats", setStats);
    laden<ClubRegistration[]>("/api/admin/club-registrations", (a) => setAntraege(a.length));
    laden<AdminOrphanedData>("/api/admin/orphaned-data", (d) => setVerwaisteKonten(d.konten));
    laden<Club[]>("/api/admin/clubs", (c) => setVereine(c.length));
    return () => {
      aktuell = false;
    };
  }, []);

  const zuErledigen = zuErledigenZeilen({ antraege, verwaisteKonten }, t);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Admin")}</h1>

      {zuErledigen.length > 0 && (
        <section className="flex flex-col gap-2">
          <h2 className="px-1 text-sm font-medium text-muted-foreground">{t("Zu erledigen")}</h2>
          <ListenGruppe>
            {zuErledigen.map((z) => (
              <ListenZeile key={z.href} href={z.href} titel={z.titel} vorschau={z.vorschau} punkt punktText={t("Offen")} />
            ))}
          </ListenGruppe>
        </section>
      )}

      {stats && (
        <KennzahlenKarte stats={stats}>
          {/* Die Zahl der aktiven Konten steht schon in AdminStats; die Zeile
              führt zur ganzen Statistik. */}
          <Link
            href="/admin/statistik"
            className="flex min-h-11 items-center justify-between gap-2 border-t pt-3 text-sm transition-colors active:bg-muted"
          >
            <span className="min-w-0 [overflow-wrap:anywhere]">
              {t("Aktiv in den letzten 30 Tagen: {n}", { n: stats.last30Days.activeAccounts })}
            </span>
            <ChevronRight className="size-5 shrink-0 text-muted-foreground" aria-hidden />
          </Link>
        </KennzahlenKarte>
      )}

      <ListenGruppe>
        <ListenZeile
          href="/admin/nutzer"
          icon={Users}
          titel={t("Nutzer")}
          vorschau={stats ? String(stats.userCount) : undefined}
        />
        <ListenZeile
          href="/admin/vereine"
          icon={Building2}
          titel={t("Vereine")}
          vorschau={vereine !== null ? String(vereine) : undefined}
        />
        <ListenZeile href="/admin/statistik" icon={BarChart3} titel={t("Statistik")} />
        <ListenZeile href="/admin/katalog" icon={Trophy} titel={t("Sportarten & Prüfungsordnungen")} />
        <ListenZeile href="/admin/sachkunde" icon={GraduationCap} titel={t("Sachkunde-Fragen")} />
        <ListenZeile href="/admin/system" icon={Settings2} titel={t("System")} untertitel={t("Testmail, Test-Push, Aufräumen")} />
      </ListenGruppe>
    </div>
  );
}
