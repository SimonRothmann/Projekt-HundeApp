"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Download, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { removeQueuedRequestsOf } from "@/lib/offline-queue";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * Auskunft und Löschung (Art. 15 und 17 DSGVO) gehören in die App und nicht
 * in eine E-Mail an den Betreiber: Ein Recht, das man erst erfragen muss,
 * übt kaum jemand aus.
 */
export function DatenExport() {
  const t = useT();
  const [exportLaeuft, setExportLaeuft] = useState(false);

  /**
   * Auskunft nach Art. 15 DSGVO - als Datei zum Behalten.
   *
   * Der Umweg über einen Blob statt eines schlichten Links ist nötig, weil
   * der Abruf den Anmelde-Token in der Kopfzeile tragen muss; ein <a href>
   * kann das nicht.
   */
  async function handleExport() {
    setExportLaeuft(true);
    try {
      const daten = await api.get<unknown>("/api/profile/export");
      const datei = new Blob([JSON.stringify(daten, null, 2)], { type: "application/json" });
      const adresse = URL.createObjectURL(datei);
      const link = document.createElement("a");
      link.href = adresse;
      link.download = `dogity-meine-daten-${new Date().toISOString().slice(0, 10)}.json`;
      // Safari lädt nur herunter, wenn das Element im Dokument hängt.
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(adresse);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Daten konnten nicht geladen werden."));
    } finally {
      setExportLaeuft(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Download className="size-5" />
          {t("Deine Daten")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          {t(
            "Alles, was Dogity über dich gespeichert hat, als Datei: Konto, Hunde, Trainings, Fährten samt Punkten, Ziele, Verein und Lernfortschritt.",
          )}
        </p>
        <Button variant="outline" className="self-start coarse:min-h-11" onClick={handleExport} disabled={exportLaeuft}>
          <Download className="size-4" />
          {exportLaeuft ? t("Wird vorbereitet…") : t("Meine Daten herunterladen")}
        </Button>
      </CardContent>
    </Card>
  );
}

export function KontoLoeschen() {
  const t = useT();
  const { user, logout } = useAuth();
  const router = useRouter();
  const [loeschenOffen, setLoeschenOffen] = useState(false);
  const [loeschPasswort, setLoeschPasswort] = useState("");
  const [loeschenLaeuft, setLoeschenLaeuft] = useState(false);

  async function handleKontoLoeschen(e: FormEvent) {
    e.preventDefault();
    setLoeschenLaeuft(true);
    try {
      await api.delete("/api/profile", { currentPassword: loeschPasswort });
      // Das Konto gibt es nicht mehr - niemand könnte offene Einträge je
      // abschicken, und sie enthalten Trainingsdaten.
      if (user) await removeQueuedRequestsOf(user.userId).catch(() => undefined);
      logout();
      router.push("/");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
      // Kein finally: Bei Erfolg ist die Seite schon unterwegs, und ein
      // Zustandswechsel auf einer verlassenen Seite bringt nichts.
      setLoeschenLaeuft(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base text-destructive">
          <Trash2 className="size-5" />
          {t("Konto löschen")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {loeschenOffen ? (
          <form onSubmit={handleKontoLoeschen} className="flex flex-col gap-3">
            <p className="text-sm text-destructive">
              {t(
                "Das lässt sich nicht rückgängig machen. Einen Hund, den du dir mit jemandem teilst, behält die andere Person.",
              )}
            </p>
            <div className="flex flex-col gap-2">
              <Label htmlFor="loeschPasswort">{t("Zur Bestätigung dein Passwort")}</Label>
              <Input
                id="loeschPasswort"
                type="password"
                required
                autoComplete="current-password"
                value={loeschPasswort}
                onChange={(e) => setLoeschPasswort(e.target.value)}
              />
            </div>
            <div className="flex flex-wrap gap-2">
              <Button type="submit" variant="destructive" disabled={loeschenLaeuft} className="coarse:min-h-11">
                <Trash2 className="size-4" />
                {loeschenLaeuft ? t("Wird gelöscht…") : t("Konto endgültig löschen")}
              </Button>
              <Button
                type="button"
                variant="ghost"
                className="coarse:min-h-11"
                onClick={() => {
                  setLoeschenOffen(false);
                  setLoeschPasswort("");
                }}
              >
                {t("Abbrechen")}
              </Button>
            </div>
          </form>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              {t(
                "Du kannst dein Konto jederzeit löschen. Deine Hunde, Trainings, Fährten, Ziele und Einstellungen werden dabei entfernt.",
              )}
            </p>
            <Button
              variant="ghost"
              className="self-start text-destructive hover:text-destructive coarse:min-h-11"
              onClick={() => setLoeschenOffen(true)}
            >
              <Trash2 className="size-4" />
              {t("Konto löschen")}
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}
