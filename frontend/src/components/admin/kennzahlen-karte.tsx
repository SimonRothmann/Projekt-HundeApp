import type { ReactNode } from "react";
import { ClipboardList, Dog, MapPin, Users, Users2, type LucideIcon } from "lucide-react";
import { useT } from "@/lib/i18n";
import type { AdminStats } from "@/lib/types";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Die fünf Plattform-Zahlen in EINER Karte (statt fünf): drei Spalten auf
 * dem Handy, fünf ab sm. Zählungen, keine Personen. `children` ist die Zeile
 * darunter (z. B. der Link zur Statistik auf der Übersicht).
 */
export function KennzahlenKarte({ stats, children }: { stats: AdminStats; children?: ReactNode }) {
  const t = useT();
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <dl className="grid grid-cols-3 gap-x-2 gap-y-4 sm:grid-cols-5">
          <Kennzahl icon={Users} label={t("Nutzer")} wert={stats.userCount} />
          <Kennzahl icon={Dog} label={t("Hunde")} wert={stats.dogCount} />
          <Kennzahl icon={Users2} label={t("Gruppen")} wert={stats.groupCount} />
          <Kennzahl icon={ClipboardList} label={t("Trainings")} wert={stats.trainingSessionCount} />
          <Kennzahl icon={MapPin} label={t("Fährten")} wert={stats.gpsTrackCount} />
        </dl>
        {children}
      </CardContent>
    </Card>
  );
}

function Kennzahl({ icon: Icon, label, wert }: { icon: LucideIcon; label: string; wert: number }) {
  return (
    <div className="flex min-w-0 flex-col items-center gap-0.5 text-center">
      <Icon className="size-5 text-primary-text" aria-hidden />
      {/* Im Quelltext Begriff vor Wert (so liest es ein Screenreader),
          gezeigt wird die Zahl über der Beschriftung. */}
      <dt className="order-3 max-w-full text-xs text-muted-foreground [overflow-wrap:anywhere]">{label}</dt>
      <dd className="order-2 text-2xl font-semibold tabular-nums">{wert}</dd>
    </div>
  );
}
