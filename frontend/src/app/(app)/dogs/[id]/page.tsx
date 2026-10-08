"use client";

import { useCallback, useState } from "react";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { MODULE } from "@/lib/types";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { laeuftFaehrte } from "@/lib/faehrte";
import { DogEditForm } from "@/components/dogs/dog-edit-form";
import { GoalsSection } from "@/components/dogs/goals-section";
import { LeistungenCard } from "@/components/dogs/leistungen-card";
import { DogHeader } from "@/components/dogs/dog-header";
import { DogActionBar } from "@/components/dogs/dog-action-bar";
import { DogStatusLine } from "@/components/dogs/dog-status-line";
import { DogTabs, reiterPanelId, reiterTabId } from "@/components/dogs/dog-tabs";
import { DiaryTab } from "@/components/dogs/diary-tab";
import { DogActionsSheet } from "@/components/dogs/dog-actions-sheet";
import { DogFaehrteSheet } from "@/components/dogs/dog-faehrte-sheet";
import { useAuth } from "@/lib/auth-context";
import { eintragIdAus, leseGesehen } from "@/lib/feedback-gesehen";
import { adresseMit, geltenderReiter, REITER_PARAMETER, ungeseheneRueckmeldungen, waehleReiter, zielStatus, type HundeReiter } from "@/lib/hundeseite";
import { tageAnzahl } from "@/lib/tagebuch";
import { usePreferences } from "@/lib/preferences-context";
import { useDogAnker } from "@/lib/use-dog-anker";
import { useDogPage } from "@/lib/use-dog-page";
import { useT } from "@/lib/i18n";

/**
 * Hundeseite: Kopf, zwei Aktionen, Statuszeile und zwei Reiter - "Plan" (Ziele)
 * und "Tagebuch" (Training erfassen, Fährten-Verlauf, kompakte Tageszeilen).
 * Verwalten (Bearbeiten, Drucken, Mitbesitzer, Archivieren, Löschen) und
 * "Fährte legen" öffnen als Sheets. Die Logik lebt in den Bausteinen unter
 * components/dogs und in lib/use-dog-page.ts.
 *
 * Reiter und Eintrag stehen in der Adresse (?tab=, ?eintrag=): Zurück, Neuladen
 * und Links aus Benachrichtigungen führen dorthin, wo man war. Die alten Anker
 * (#trainingsplan, #training-erfassen, #faehrte-aufnehmen) setzt useDogAnker um.
 */
export default function DogDetailPage() {
  const { id } = useParams<{ id: string }>();
  // key: Beim Wechsel zu einem anderen Hund (Chips im Kopf) beginnt die Seite
  // mit leerem Zustand - sonst blieben offenes Formular, Bearbeiten und
  // Tagebuchfilter des vorigen Hundes stehen.
  return <DogPage key={id} id={id} />;
}

function DogPage({ id }: { id: string }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const suchparameter = useSearchParams();
  const { user } = useAuth();
  const { moduleEnabled } = usePreferences();
  // Benachrichtigungen zu Feedback führen auf den Eintrag (?eintrag=). Nur eine
  // Id in GUID-Form wird übernommen; alles andere ist, als stünde nichts da.
  const eintragId = eintragIdAus(suchparameter.get("eintrag"));
  const { dog, sessions, sports, goals, isOwner, owners, myDogs, nichtGefunden, showAllHistory, dogSportIds, frischGeladen, loadAll, loadOlderSessions } =
    useDogPage(id, eintragId);

  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState(false);
  const [menuOffen, setMenuOffen] = useState(false);
  const [faehrteOffen, setFaehrteOffen] = useState(false);
  // Zählt die Änderungen, nach denen der Fährten-Verlauf neu zu laden ist (neue
  // Fährte, neuer Ablauf, gelöscht). Ein Zähler statt der Trainingsliste als
  // Auslöser: Die Liste wechselt schon beim Öffnen der Seite mehrmals (Cache,
  // dann frische Daten) und ergäbe jedes Mal einen eigenen Abruf.
  const [faehrtenStand, setFaehrtenStand] = useState(0);
  // Welches Feedback beim Öffnen der Seite schon gesehen war (eingefroren, damit
  // "Neu" während des Besuchs stehen bleibt), und was seither aufgeklappt wurde.
  const [gesehenBeiStart] = useState<ReadonlySet<string>>(() => new Set(leseGesehen()));
  const [jetztGesehen, setJetztGesehen] = useState<ReadonlySet<string>>(() => new Set());
  const onGesehen = useCallback(
    (kennung: string) => setJetztGesehen((vorher) => (vorher.has(kennung) ? vorher : new Set(vorher).add(kennung))),
    [],
  );

  // Sportarten, die dem Tagebuch angeboten werden. Leere Auswahl heißt
  // "keine Einschränkung" - und solange noch nichts geladen ist ebenfalls,
  // damit die Liste nicht kurz leer aufblitzt.
  const angeboteneSportarten =
    dogSportIds && dogSportIds.length > 0 ? sports.filter((s) => dogSportIds.includes(s.id)) : sports;
  // Fährte anbieten, wenn das Modul an ist UND der Hund Fährte läuft. Beides
  // zusammen, weil die GPS-Aufzeichnung auch für Spaziergänge taugt: Wer sie
  // dafür nutzt, darf sie behalten, ohne "Fährte" als Sportart anzugeben - dann
  // lässt er das Modul an und wählt die Sportart ab. Erkannt am Code der
  // Sportart, nicht am Namen - Namen ändern sich, Codes nicht.
  const zeigtFaehrte = moduleEnabled(MODULE.faehrte) && laeuftFaehrte(dogSportIds, sports);
  // Der Standard-Reiter wird einmal festgelegt, sobald die frischen Daten da
  // sind, und bleibt dann stehen: Trägt jemand sein einziges laufendes Ziel als
  // erreicht ein, soll der Plan nicht unter den Händen verschwinden. (Zustand
  // während des Renderns angleichen - so gibt es keinen Frame mit dem alten.)
  const [standardReiter, setStandardReiter] = useState<HundeReiter | null>(null);
  if (frischGeladen && standardReiter === null) {
    setStandardReiter(waehleReiter(null, goals, { eintrag: eintragId !== null }));
  }
  const reiter = geltenderReiter(suchparameter.get(REITER_PARAMETER), standardReiter, goals, { eintrag: eintragId !== null });

  function aendereAdresse(aenderungen: Record<string, string | null>) {
    router.replace(adresseMit(pathname, suchparameter.toString(), aenderungen), { scroll: false });
  }

  useDogAnker(dog !== null, (wunsch) => {
    if (wunsch.formular) setShowForm(true);
    if (wunsch.faehrte && zeigtFaehrte) setFaehrteOffen(true);
    // Auch ohne neuen Reiter: die Adresse verliert dabei das Fragment.
    aendereAdresse(wunsch.reiter ? { [REITER_PARAMETER]: wunsch.reiter } : {});
  });

  async function faehrteGeaendert() {
    await loadAll();
    setFaehrtenStand((n) => n + 1);
  }

  async function handleTrainingSaved(offline: boolean) {
    setShowForm(false);
    // Offline gespeicherte Trainings liegen nur in der Warteschlange - ein
    // Server-Reload würde sie nicht enthalten und nur verwirren.
    if (!offline) await loadAll();
  }

  if (nichtGefunden)
    return (
      <Card>
        <CardContent className="flex flex-col items-start gap-3 py-8">
          <p>{t("Diesen Hund gibt es nicht mehr, oder du hast keinen Zugriff darauf.")}</p>
          <Link href="/dogs" className={buttonVariants({ variant: "outline", size: "sm" })}>
            {t("Zu meinen Hunden")}
          </Link>
        </CardContent>
      </Card>
    );
  if (!dog) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  const neueRueckmeldungen = ungeseheneRueckmeldungen(sessions, new Set([...gesehenBeiStart, ...jetztGesehen]), isOwner);
  const wechseln = (neu: HundeReiter) => aendereAdresse({ [REITER_PARAMETER]: neu });

  return (
    <div className="flex flex-col gap-4">
      <DogHeader dog={dog} meineHunde={myDogs} gehoertMir={isOwner} onVerwalten={() => setMenuOffen(true)} />

      {editing && <DogEditForm dog={dog} onSaved={loadAll} onCancel={() => setEditing(false)} />}

      <DogActionBar
        zeigtFaehrte={zeigtFaehrte}
        onTraining={() => {
          setShowForm(true);
          wechseln("tagebuch");
        }}
        onFaehrte={() => setFaehrteOffen(true)}
      />

      <DogStatusLine
        ziel={zielStatus(goals)}
        neuesFeedback={neueRueckmeldungen.length > 0}
        onZiel={() => wechseln("plan")}
        onFeedback={() => aendereAdresse({ [REITER_PARAMETER]: "tagebuch", eintrag: neueRueckmeldungen[0].id })}
      />

      <DogTabs reiter={reiter} tage={tageAnzahl(sessions)} onChange={wechseln} />

      {/* Beide Reiter bleiben im Baum, der andere ist nur versteckt: Ein halb
          ausgefülltes Trainingsformular oder ein offenes Ziel-Formular geht
          beim Hin- und Herschalten nicht verloren. Das display liegt auf dem
          Innenelement, damit "hidden" nicht von einer Klasse überstimmt wird. */}
      <div role="tabpanel" id={reiterPanelId("plan")} aria-labelledby={reiterTabId("plan")} hidden={reiter !== "plan"}>
        <div className="flex flex-col gap-6">
          <GoalsSection dogId={id} dogName={dog.name} sports={angeboteneSportarten} goals={goals} onChanged={loadAll} />
          <LeistungenCard goals={goals} onChanged={loadAll} />
        </div>
      </div>
      <div role="tabpanel" id={reiterPanelId("tagebuch")} aria-labelledby={reiterTabId("tagebuch")} hidden={reiter !== "tagebuch"}>
        <DiaryTab
          dogId={id}
          dogName={dog.name}
          isOwner={isOwner}
          sports={angeboteneSportarten}
          goals={goals}
          sessions={sessions}
          formularOffen={showForm}
          onFormularSchliessen={() => setShowForm(false)}
          onTrainingGespeichert={handleTrainingSaved}
          faehrteAn={zeigtFaehrte}
          faehrtenStand={faehrtenStand}
          onChanged={faehrteGeaendert}
          onLoadOlder={showAllHistory ? null : loadOlderSessions}
          // Nur im sichtbaren Reiter: Im versteckten gälte das Feedback als
          // gesehen, und die Karte würde in einem 0x0-Container aufgebaut.
          fokusEintrag={frischGeladen && reiter === "tagebuch" ? eintragId : null}
          gesehenBeiStart={gesehenBeiStart}
          onGesehen={onGesehen}
        />
      </div>

      <DogActionsSheet
        dog={dog}
        isOwner={isOwner}
        owners={owners}
        currentUserId={user?.userId}
        open={menuOffen}
        onOpenChange={setMenuOffen}
        onEdit={() => setEditing(true)}
        onChanged={loadAll}
      />
      {zeigtFaehrte && (
        <DogFaehrteSheet dogId={id} open={faehrteOffen} onOpenChange={setFaehrteOffen} onSaved={faehrteGeaendert} />
      )}
    </div>
  );
}
