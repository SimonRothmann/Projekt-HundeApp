"use client";

import { useCallback, useEffect, useState, useSyncExternalStore } from "react";
import { AlertTriangle, ChevronDown } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  eintragErneutSenden,
  eintragVerwerfen,
  listProblemRequests,
  warteschlangeAbonnieren,
  warteschlangeStand,
  zustandVon,
  type QueuedRequest,
} from "@/lib/offline-queue";
import { useRueckfrage } from "@/components/ui/rueckfrage";
import { useT } from "@/lib/i18n";

/**
 * Einträge der Offline-Warteschlange, die nicht von selbst hinausgehen: vom Server abgelehnt oder nach
 * mehreren Serverfehlern aufgegeben. Sie bleiben erhalten, bis die Person sie erneut sendet oder - nach
 * Rückfrage - verwirft. Ohne diesen Hinweis ginge etwa eine offline gelegte Fährte still verloren.
 *
 * @param onErneutSenden stößt den Abgleich an, damit der Eintrag gleich einen Versuch bekommt.
 */
export function OfflineEintraegeHinweis({ onErneutSenden }: { onErneutSenden: () => void }) {
  const t = useT();
  const rueckfrage = useRueckfrage();
  const [eintraege, setEintraege] = useState<QueuedRequest[]>([]);
  const [offen, setOffen] = useState(false);
  // Jede Änderung der Warteschlange in diesem Tab; andere Tabs: beim Zurückkehren in die App.
  const stand = useSyncExternalStore(warteschlangeAbonnieren, warteschlangeStand, () => 0);

  const laden = useCallback(async () => {
    try {
      setEintraege(await listProblemRequests());
    } catch {
      // Warteschlange nicht lesbar: kein Hinweis statt einer Fehlermeldung.
    }
  }, []);

  useEffect(() => {
    // Die Warteschlange liegt in IndexedDB (externe Quelle), das Setzen geschieht erst in der Antwort.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [laden, stand]);

  useEffect(() => {
    const sichtbar = () => {
      if (document.visibilityState === "visible") void laden();
    };
    document.addEventListener("visibilitychange", sichtbar);
    return () => document.removeEventListener("visibilitychange", sichtbar);
  }, [laden]);

  if (eintraege.length === 0) return null;

  async function verwerfen(item: QueuedRequest) {
    if (!(await rueckfrage({ titel: t("„{label}“ endgültig verwerfen?", { label: item.label }), text: t("Die Daten gehen verloren."), bestaetigen: t("Verwerfen"), gefaehrlich: true }))) return;
    try {
      await eintragVerwerfen(item.id);
    } catch {
      toast.error(t("Das hat nicht geklappt."));
    }
  }

  async function erneutSenden(item: QueuedRequest) {
    try {
      await eintragErneutSenden(item.id);
      onErneutSenden();
    } catch {
      toast.error(t("Das hat nicht geklappt."));
    }
  }

  return (
    <div className="relative z-10 px-4 pt-3 md:px-8 print:hidden">
      <div role="status" className="rounded-lg border border-warnung/40 bg-warnung/10 text-sm text-warnung-text">
        <button
          type="button"
          onClick={() => setOffen((o) => !o)}
          aria-expanded={offen}
          className="flex min-h-11 w-full items-center gap-2.5 px-3 py-2 text-left"
        >
          <AlertTriangle className="size-4 shrink-0" aria-hidden />
          <span className="min-w-0 flex-1 break-words font-medium">
            {eintraege.length === 1
              ? t("Ein Eintrag ist nicht übertragen worden.")
              : t("{n} Einträge sind nicht übertragen worden.", { n: eintraege.length })}
          </span>
          <ChevronDown className={`size-4 shrink-0 transition-transform ${offen ? "rotate-180" : ""}`} aria-hidden />
        </button>
        {offen && (
          <ul className="divide-y divide-warnung/25 border-t border-warnung/25 px-3">
            {eintraege.map((item) => (
              <li key={item.id} className="flex flex-col gap-2 py-3">
                <div className="min-w-0">
                  <p className="break-words font-medium">{item.label}</p>
                  <p className="break-words text-xs">
                    {zustandVon(item) === "abgelehnt"
                      ? t("Der Server hat den Eintrag abgelehnt: {meldung}", { meldung: item.abgelehnt?.meldung ?? "" })
                      : t("Der Server antwortet mit einem Fehler, der Eintrag wird nicht mehr von selbst gesendet. {meldung}", {
                          meldung: item.letzteMeldung ?? "",
                        })}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="outline" onClick={() => void erneutSenden(item)} className="coarse:min-h-11">
                    {t("Erneut senden")}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => void verwerfen(item)}
                    className="text-destructive hover:text-destructive coarse:min-h-11"
                  >
                    {t("Verwerfen")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
