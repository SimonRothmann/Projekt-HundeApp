"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { AdminQuizOption, AdminQuizQuestion, QuizCatalog } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { AlertTriangle, ChevronDown, ChevronRight, GraduationCap, Plus, RotateCcw, Search, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
/**
 * Verwaltung der Sachkunde-Fragen: alles ansehen und von Hand überarbeiten.
 *
 * Die Kataloge stammen aus einer PDF-Auswertung. Die kommt weit, aber nicht
 * überall hin - verschluckte Leerzeichen, Trennstriche mitten im Wort, Reste
 * des Seitenlayouts. Solche Stellen zieht nur ein Mensch glatt.
 *
 * Zwei Dinge tragen die Ansicht:
 * - **Auffälligkeiten**: Der Server markiert Textstellen, bei denen sich das
 *   Nachsehen lohnt. Das ersetzt kein Lesen, spart aber das Durchgehen von
 *   112 Fragen auf gut Glück.
 * - **"von Hand bearbeitet"**: Wer hier speichert, hat ab dann das Sagen - der
 *   Seeder überschreibt die Frage beim nächsten Start nicht mehr. Ohne das
 *   wäre jede Korrektur beim nächsten Deploy wieder weg.
 */

const ALLE = "__alle__";

type Entwurf = {
  text: string;
  sampleSolution: string;
  options: (AdminQuizOption & { neu?: boolean })[];
};

export function SachkundeSection() {
  const t = useT();
  const [catalogs, setCatalogs] = useState<QuizCatalog[]>([]);
  const [katalog, setKatalog] = useState<string>(ALLE);
  const [komplex, setKomplex] = useState<string>(ALLE);
  const [suche, setSuche] = useState("");
  const [nurAuffaellig, setNurAuffaellig] = useState(false);
  const [nurBearbeitet, setNurBearbeitet] = useState(false);

  const [fragen, setFragen] = useState<AdminQuizQuestion[] | null>(null);
  const [offen, setOffen] = useState<string | null>(null);
  const [entwurf, setEntwurf] = useState<Entwurf | null>(null);
  const [speichert, setSpeichert] = useState(false);

  useEffect(() => {
    api
      .get<QuizCatalog[]>("/api/sachkunde/catalogs")
      .then(setCatalogs)
      .catch(() => setCatalogs([]));
  }, []);

  const laden = useCallback(async () => {
    const p = new URLSearchParams();
    if (katalog !== ALLE) p.set("catalog", katalog);
    if (komplex !== ALLE) p.set("section", komplex);
    if (suche.trim()) p.set("search", suche.trim());
    if (nurAuffaellig) p.set("onlyFlagged", "true");
    if (nurBearbeitet) p.set("onlyEdited", "true");

    try {
      setFragen(await api.get<AdminQuizQuestion[]>(`/api/admin/sachkunde/questions?${p}`));
    } catch (err) {
      setFragen([]);
      toast.error(err instanceof ApiError ? err.message : t("Die Fragen konnten nicht geladen werden."));
    }
  // t bewusst nicht in der Liste - siehe die Effekte oben: Der
  // Uebersetzer wird nur im Fehlerfall gebraucht.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [katalog, komplex, suche, nurAuffaellig, nurBearbeitet]);

  useEffect(() => {
    // Erster Abruf und jede Filteränderung (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
  }, [laden]);

  const komplexe = catalogs
    .filter((c) => katalog === ALLE || c.code === katalog)
    .flatMap((c) => c.sections)
    .filter((s, i, alle) => alle.findIndex((x) => x.key === s.key) === i);

  function aufklappen(frage: AdminQuizQuestion) {
    if (offen === frage.id) {
      setOffen(null);
      setEntwurf(null);
      return;
    }
    setOffen(frage.id);
    setEntwurf({
      text: frage.text,
      sampleSolution: frage.sampleSolution ?? "",
      options: frage.options.map((o) => ({ ...o })),
    });
  }

  async function speichern(frage: AdminQuizQuestion) {
    if (!entwurf) return;
    setSpeichert(true);
    try {
      await api.put(`/api/admin/sachkunde/questions/${frage.id}`, {
        text: entwurf.text,
        sampleSolution: entwurf.sampleSolution || null,
        options: entwurf.options.map((o) => ({
          id: o.neu ? null : o.id,
          kind: o.kind,
          text: o.text,
          isCorrect: o.isCorrect,
          matchKey: o.matchKey,
          imageName: o.imageName,
        })),
      });
      toast.success(`${frage.number} gespeichert – der Katalog überschreibt sie nicht mehr.`);
      setOffen(null);
      setEntwurf(null);
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Speichern fehlgeschlagen."));
    } finally {
      setSpeichert(false);
    }
  }

  async function zuruecknehmen(frage: AdminQuizQuestion) {
    try {
      await api.post(`/api/admin/sachkunde/questions/${frage.id}/revert`);
      toast.success(t("Zurückgenommen – die Katalogfassung kommt beim nächsten Start der App zurück."));
      setOffen(null);
      setEntwurf(null);
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Zurücknehmen fehlgeschlagen."));
    }
  }

  function zeileAendern(index: number, patch: Partial<AdminQuizOption>) {
    setEntwurf((e) =>
      e ? { ...e, options: e.options.map((o, i) => (i === index ? { ...o, ...patch } : o)) } : e,
    );
  }

  const auffaellig = (fragen ?? []).filter(
    (f) => f.flags.length > 0 || f.options.some((o) => o.flags.length > 0),
  ).length;
  const bearbeitet = (fragen ?? []).filter((f) => f.editedAt).length;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <GraduationCap className="size-5" />
          Sachkunde-Fragen
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
{t("Die Kataloge kommen aus einer PDF-Auswertung – einzelne Texte brauchen eine Hand. Wer hier speichert, hat ab dann das Sagen: Der Katalog überschreibt diese Frage beim nächsten Start der App nicht mehr.")}
        </p>

        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-2">
            <Select value={katalog} onValueChange={(v) => { setKatalog(v ?? ALLE); setKomplex(ALLE); }}>
              <SelectTrigger className="w-full sm:w-64">
                <SelectValue placeholder="Katalog" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALLE}>{t("Alle Kataloge")}</SelectItem>
                {catalogs.map((c) => (
                  <SelectItem key={c.code} value={c.code}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={komplex} onValueChange={(v) => setKomplex(v ?? ALLE)}>
              <SelectTrigger className="w-full sm:w-64">
                <SelectValue placeholder="Komplex" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALLE}>{t("Alle Komplexe")}</SelectItem>
                {komplexe.map((s) => (
                  <SelectItem key={s.key} value={s.key}>
                    {s.key} – {s.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder={t("In Fragen, Antworten und Lösungen suchen…")}
              value={suche}
              onChange={(e) => setSuche(e.target.value)}
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant={nurAuffaellig ? "default" : "outline"}
              onClick={() => setNurAuffaellig((v) => !v)}
            >
              <AlertTriangle className="size-4" />
{t("Nur auffällige")}
            </Button>
            <Button
              size="sm"
              variant={nurBearbeitet ? "default" : "outline"}
              onClick={() => setNurBearbeitet((v) => !v)}
            >
              Nur bearbeitete
            </Button>
          </div>
        </div>

        {fragen === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <>
            <p className="text-xs text-muted-foreground tabular-nums">
              {fragen.length} Fragen
              {auffaellig > 0 && <> · {auffaellig} auffällig</>}
              {bearbeitet > 0 && <> · {bearbeitet} von Hand bearbeitet</>}
            </p>

            <ul className="flex flex-col gap-1.5">
              {fragen.map((frage) => {
                const istOffen = offen === frage.id;
                const hatFlags = frage.flags.length > 0 || frage.options.some((o) => o.flags.length > 0);

                return (
                  <li key={frage.id} className="min-w-0 rounded-lg border border-border/60">
                    <button
                      type="button"
                      onClick={() => aufklappen(frage)}
                      className="flex w-full min-w-0 items-start gap-2 px-3 py-2.5 text-left coarse:min-h-11"
                    >
                      {istOffen ? (
                        <ChevronDown className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                      ) : (
                        <ChevronRight className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                      )}
                      <span className="flex min-w-0 flex-1 flex-col gap-1">
                        <span className="flex flex-wrap items-center gap-1.5">
                          <Badge variant="outline" className="text-xs">
                            {frage.number}
                          </Badge>
                          {frage.kind !== "SingleChoice" && (
                            <Badge variant="secondary" className="text-xs">
                              {artName(frage.kind)}
                            </Badge>
                          )}
                          {frage.editedAt && (
                            <Badge variant="secondary" className="text-xs">
{t("von Hand")}
                            </Badge>
                          )}
                          {hatFlags && (
                            <Badge variant="outline" className="border-amber-500/60 text-xs text-amber-600 dark:text-amber-400">
{t("auffällig")}
                            </Badge>
                          )}
                        </span>
                        <span className="min-w-0 text-sm [overflow-wrap:anywhere]">{frage.text}</span>
                      </span>
                    </button>

                    {istOffen && entwurf && (
                      <div className="flex flex-col gap-3 border-t border-border/60 px-3 py-3">
                        {frage.flags.length > 0 && <Hinweise flags={frage.flags} />}

                        <div className="flex flex-col gap-1.5">
                          <Label htmlFor={`text-${frage.id}`}>Fragestellung</Label>
                          <textarea
                            id={`text-${frage.id}`}
                            className="min-h-20 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm"
                            value={entwurf.text}
                            onChange={(e) => setEntwurf({ ...entwurf, text: e.target.value })}
                          />
                        </div>

                        {frage.imageName && (
                          // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public.
                          <img
                            src={`/sachkunde/${frage.imageName}`}
                            alt={t("Abbildung zur Frage")}
                            className="h-auto w-full max-w-xs rounded-md border border-border/60 bg-white"
                          />
                        )}

                        {(frage.kind === "FreeText" || frage.kind === "Assignment") && (
                          <div className="flex flex-col gap-1.5">
                            <Label htmlFor={`loesung-${frage.id}`}>{t("Musterlösung")}</Label>
                            <textarea
                              id={`loesung-${frage.id}`}
                              className="min-h-16 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm"
                              value={entwurf.sampleSolution}
                              onChange={(e) => setEntwurf({ ...entwurf, sampleSolution: e.target.value })}
                            />
                          </div>
                        )}

                        <ZeilenListe
                          entwurf={entwurf}
                          frageArt={frage.kind}
                          onAendern={zeileAendern}
                          onEntfernen={(i) =>
                            setEntwurf({ ...entwurf, options: entwurf.options.filter((_, x) => x !== i) })
                          }
                          onHinzufuegen={(kind) =>
                            setEntwurf({
                              ...entwurf,
                              options: [
                                ...entwurf.options,
                                {
                                  id: crypto.randomUUID(),
                                  neu: true,
                                  kind,
                                  text: "",
                                  isCorrect: false,
                                  matchKey: null,
                                  imageName: null,
                                  sortOrder: entwurf.options.length + 1,
                                  flags: [],
                                },
                              ],
                            })
                          }
                        />

                        <div className="flex flex-wrap gap-2">
                          <Button size="sm" disabled={speichert} onClick={() => speichern(frage)}>
{t("Speichern")}
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => aufklappen(frage)}>
{t("Abbrechen")}
                          </Button>
                          {frage.editedAt && (
                            <Button size="sm" variant="outline" onClick={() => zuruecknehmen(frage)}>
                              <RotateCcw className="size-4" />
{t("Katalogfassung zurückholen")}
                            </Button>
                          )}
                        </div>

                        {frage.editedAt && (
                          <p className="text-xs text-muted-foreground">
                            Von Hand bearbeitet am {new Date(frage.editedAt).toLocaleDateString("de-DE")}. Der
                            Katalog überschreibt diese Frage nicht mehr.
                          </p>
                        )}
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>

            {fragen.length === 0 && (
              <p className="text-sm text-muted-foreground">{t("Keine Frage passt zu diesen Filtern.")}</p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function artName(kind: AdminQuizQuestion["kind"]): string {
  switch (kind) {
    case "MultipleChoice":
      return "Mehrfachauswahl";
    case "Assignment":
      return "Zuordnung";
    case "FreeText":
      return "Freitext";
    default:
      return "Auswahl";
  }
}

function Hinweise({ flags }: { flags: string[] }) {
  return (
    <p className="flex flex-wrap items-center gap-1.5 rounded-md border border-amber-500/40 bg-amber-500/10 px-2.5 py-1.5 text-xs text-amber-700 dark:text-amber-300">
      <AlertTriangle className="size-3.5 shrink-0" />
      {flags.join(" · ")}
    </p>
  );
}

function ZeilenListe({
  entwurf,
  frageArt,
  onAendern,
  onEntfernen,
  onHinzufuegen,
}: {
  entwurf: Entwurf;
  frageArt: AdminQuizQuestion["kind"];
  onAendern: (index: number, patch: Partial<AdminQuizOption>) => void;
  onEntfernen: (index: number) => void;
  onHinzufuegen: (kind: AdminQuizOption["kind"]) => void;
}) {
  const t = useT();
  const zuordnung = frageArt === "Assignment";

  return (
    <div className="flex flex-col gap-2">
      <Label>{zuordnung ? t("Begriffe und Beschriftungen") : "Antworten"}</Label>

      {entwurf.options.map((zeile, index) => (
        <div key={zeile.id} className="flex min-w-0 flex-col gap-1.5 rounded-md border border-border/50 p-2">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline" className="text-xs">
              {zeilenName(zeile.kind)}
            </Badge>

            {zeile.kind === "Answer" && !zuordnung && (
              <label className="flex items-center gap-1.5 text-xs">
                <input
                  type="checkbox"
                  className="size-4"
                  checked={zeile.isCorrect}
                  onChange={(e) => onAendern(index, { isCorrect: e.target.checked })}
                />
                richtig
              </label>
            )}

            {zeile.kind !== "Answer" && (
              <label className="flex items-center gap-1.5 text-xs">
                Schlüssel
                <Input
                  className="h-7 w-16"
                  value={zeile.matchKey ?? ""}
                  onChange={(e) => onAendern(index, { matchKey: e.target.value })}
                />
              </label>
            )}

            <Button
              size="icon"
              variant="ghost"
              className="ml-auto size-7 shrink-0"
              title={t("Zeile entfernen")}
              onClick={() => onEntfernen(index)}
            >
              <Trash2 className="size-3.5" />
            </Button>
          </div>

          <Input value={zeile.text} onChange={(e) => onAendern(index, { text: e.target.value })} />

          {zeile.imageName && (
            // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public.
            <img
              src={`/sachkunde/${zeile.imageName}`}
              alt={`Abbildung zu Antwort ${zeile.text}`}
              className="h-auto w-full max-w-[10rem] rounded border border-border/60 bg-white"
            />
          )}

          {zeile.flags.length > 0 && <Hinweise flags={zeile.flags} />}
        </div>
      ))}

      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => onHinzufuegen(zuordnung ? "Term" : "Answer")}>
          <Plus className="size-4" />
          {zuordnung ? "Begriff" : "Antwort"}
        </Button>
        {zuordnung && (
          <Button size="sm" variant="outline" onClick={() => onHinzufuegen("Label")}>
            <Plus className="size-4" />
            Beschriftung
          </Button>
        )}
      </div>
    </div>
  );
}

function zeilenName(kind: AdminQuizOption["kind"]): string {
  switch (kind) {
    case "Term":
      return "Begriff";
    case "Label":
      return "Beschriftung";
    default:
      return "Antwort";
  }
}
