"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ChevronDown, ChevronLeft, ChevronRight, KeyRound, Lock, Trash2, Unlock } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { AdminUser, AdminUserPage } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";

const SEITENGROESSE = 50;
/** Wartezeit nach dem letzten Tastendruck, bevor gesucht wird. */
const ENTPRELLEN_MS = 300;

/**
 * Nutzerverwaltung: Suche, eine Zeile je Person, Antippen klappt die
 * Aktionen auf (Passwort setzen, sperren, löschen) - als große, beschriftete
 * Knöpfe statt dreier Symbole nebeneinander.
 *
 * `fokusId` kommt aus dem Link einer Passwort-Reset-Benachrichtigung
 * (?resetUser=<id>). Die Person steht dann womöglich nicht auf Seite 1; statt
 * die Liste zu durchblättern, fragt die Liste sie direkt ab (das Backend
 * findet bei einer Id genau dieses Konto) und zeigt sie mit offenem
 * Passwort-Formular. Eine Hinweiszeile erlaubt den Weg zurück zu allen.
 */
export function NutzerSection({ fokusId = null }: { fokusId?: string | null }) {
  const t = useT();
  const router = useRouter();
  const [eingabe, setEingabe] = useState("");
  const [begriff, setBegriff] = useState("");
  const [fokus, setFokus] = useState<string | null>(fokusId);
  const [seite, setSeite] = useState(1);
  const [neuLaden, setNeuLaden] = useState(0);
  const [userPage, setUserPage] = useState<AdminUserPage | null>(null);
  // Welche Zeile aufgeklappt ist und bei wem das Passwort-Formular offen ist.
  const [offenId, setOffenId] = useState<string | null>(fokusId);
  const [pwUserId, setPwUserId] = useState<string | null>(fokusId);
  const [newPassword, setNewPassword] = useState("");
  const [settingPassword, setSettingPassword] = useState(false);

  // Entprellen: erst gesucht wird, wenn kurz nichts mehr getippt wurde; eine
  // neue Suche beginnt immer auf Seite 1. Ändert sich der Begriff gar nicht
  // (Seite geöffnet, nur ein Leerzeichen getippt), bleibt die Liste, wo sie ist.
  useEffect(() => {
    const neu = eingabe.trim();
    if (neu === begriff) return;
    const timer = setTimeout(() => {
      setBegriff(neu);
      setSeite(1);
    }, ENTPRELLEN_MS);
    return () => clearTimeout(timer);
  }, [eingabe, begriff]);

  const suche = fokus ?? begriff;

  useEffect(() => {
    let aktuell = true;
    const parameter = new URLSearchParams({ page: String(seite), pageSize: String(SEITENGROESSE) });
    if (suche) parameter.set("suche", suche);
    api
      .get<AdminUserPage>(`/api/admin/users?${parameter.toString()}`)
      .then((daten) => {
        if (!aktuell) return;
        // Nach dem Löschen der letzten Person einer Seite: eine Seite zurück.
        if (daten.users.length === 0 && seite > 1) {
          setSeite(seite - 1);
          return;
        }
        setUserPage(daten);
      })
      .catch((err) => {
        if (aktuell) toast.error(err instanceof ApiError ? err.message : t("Nutzer konnten nicht geladen werden."));
      });
    return () => {
      aktuell = false;
    };
    // t bewusst nicht in der Liste: Der Uebersetzer wird nur im Fehlerfall
    // gebraucht - ein Sprachwechsel soll die Liste nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seite, suche, neuLaden]);

  function suchen(wert: string) {
    setEingabe(wert);
    // Wer tippt, will nicht mehr die eine Person aus dem Link.
    fokusBeenden();
  }

  function alleZeigen() {
    fokusBeenden();
    setSeite(1);
  }

  /** Auch aus der Adresse: Sonst brächte Neuladen die eine Person zurück. */
  function fokusBeenden() {
    if (fokus === null) return;
    setFokus(null);
    router.replace("/admin/nutzer", { scroll: false });
  }

  function zeileUmschalten(id: string) {
    setOffenId((aktuell) => (aktuell === id ? null : id));
    setPwUserId(null);
    setNewPassword("");
  }

  async function handleToggleLock(user: AdminUser) {
    try {
      await api.post(`/api/admin/users/${user.id}/${user.isLockedOut ? "unlock" : "lock"}`);
      toast.success(user.isLockedOut ? t("Konto entsperrt.") : t("Konto gesperrt."));
      setNeuLaden((n) => n + 1);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Aktion fehlgeschlagen."));
    }
  }

  async function handleDeleteUser(user: AdminUser) {
    if (!window.confirm(t("{name} wirklich endgültig löschen?", { name: anzeigeName(user) }))) return;
    try {
      await api.delete(`/api/admin/users/${user.id}`);
      toast.success(t("Nutzer gelöscht."));
      setOffenId(null);
      setPwUserId(null);
      setNeuLaden((n) => n + 1);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
    }
  }

  async function handleSetPassword(user: AdminUser) {
    if (newPassword.length < 8) {
      toast.error(t("Passwort muss mindestens 8 Zeichen lang sein."));
      return;
    }
    setSettingPassword(true);
    try {
      await api.post(`/api/admin/users/${user.id}/set-password`, { newPassword });
      toast.success(t("Neues Passwort für {email} gesetzt. Bitte dem Nutzer sicher mitteilen.", { email: user.email }));
      setPwUserId(null);
      setNewPassword("");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Passwort konnte nicht gesetzt werden."));
    } finally {
      setSettingPassword(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Input
        type="search"
        value={eingabe}
        onChange={(e) => suchen(e.target.value)}
        maxLength={TEXTLAENGE.nutzerSuche}
        placeholder={t("Name oder E-Mail suchen")}
        aria-label={t("Name oder E-Mail suchen")}
        autoComplete="off"
        className="h-11"
      />

      {fokus && (
        <div className="flex flex-col gap-2 rounded-xl bg-primary/5 px-4 py-3 text-sm ring-1 ring-primary/30">
          <p>{t("Passwort-Reset angefordert: Es wird nur diese Person gezeigt.")}</p>
          <Button variant="outline" className="h-11 self-start" onClick={alleZeigen}>
            {t("Alle Nutzer anzeigen")}
          </Button>
        </div>
      )}

      {userPage === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : (
        <>
          <p className="text-xs text-muted-foreground">
            {suche
              ? t("{n} Treffer · Seite {seite} / {gesamt}", {
                  n: userPage.totalCount,
                  seite: userPage.page,
                  gesamt: Math.max(userPage.totalPages, 1),
                })
              : t("{n} gesamt · Seite {seite} / {gesamt}", {
                  n: userPage.totalCount,
                  seite: userPage.page,
                  gesamt: Math.max(userPage.totalPages, 1),
                })}
          </p>

          {userPage.users.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Keine Nutzer gefunden.")}</p>
          ) : (
            <ListenGruppe>
              {userPage.users.map((u) => (
                <NutzerZeile
                  key={u.id}
                  user={u}
                  offen={offenId === u.id}
                  passwortOffen={pwUserId === u.id}
                  newPassword={newPassword}
                  settingPassword={settingPassword}
                  onToggle={() => zeileUmschalten(u.id)}
                  onPasswortForm={(offen) => {
                    setPwUserId(offen ? u.id : null);
                    setNewPassword("");
                  }}
                  onPasswortAendern={setNewPassword}
                  onPasswortSetzen={() => handleSetPassword(u)}
                  onLock={() => handleToggleLock(u)}
                  onDelete={() => handleDeleteUser(u)}
                />
              ))}
            </ListenGruppe>
          )}

          {userPage.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="outline"
                className="h-11 flex-1"
                disabled={seite <= 1}
                onClick={() => setSeite(seite - 1)}
              >
                <ChevronLeft className="size-4" />
                {t("Vorherige Seite")}
              </Button>
              <Button
                variant="outline"
                className="h-11 flex-1"
                disabled={seite >= userPage.totalPages}
                onClick={() => setSeite(seite + 1)}
              >
                {t("Nächste Seite")}
                <ChevronRight className="size-4" />
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

/**
 * Rollennamen für die Badges. "USER" hat jede Person - als Badge nur Lärm.
 * Unbekannte Rollen erscheinen, wie der Server sie liefert.
 */
function sichtbareRollen(rollen: string[], t: ReturnType<typeof useT>): string[] {
  const namen: Record<string, string> = {
    ADMIN: t("Admin"),
    TRAINER: t("Trainer:in"),
    CLUB_ADMIN: t("Vereinsverwaltung"),
    JUDGE: t("Richter:in"),
  };
  return rollen.filter((r) => r !== "USER").map((r) => namen[r] ?? r);
}

function anzeigeName(user: AdminUser): string {
  const name = `${user.firstName} ${user.lastName}`.trim();
  return name ? `${name} (${user.email})` : user.email;
}

function NutzerZeile({
  user,
  offen,
  passwortOffen,
  newPassword,
  settingPassword,
  onToggle,
  onPasswortForm,
  onPasswortAendern,
  onPasswortSetzen,
  onLock,
  onDelete,
}: {
  user: AdminUser;
  offen: boolean;
  passwortOffen: boolean;
  newPassword: string;
  settingPassword: boolean;
  onToggle: () => void;
  onPasswortForm: (offen: boolean) => void;
  onPasswortAendern: (wert: string) => void;
  onPasswortSetzen: () => void;
  onLock: () => void;
  onDelete: () => void;
}) {
  const t = useT();
  const name = `${user.firstName} ${user.lastName}`.trim();
  const rollen = sichtbareRollen(user.roles, t);

  return (
    <div className={cn(passwortOffen && "bg-primary/5")}>
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={offen}
        className="flex min-h-[3.25rem] w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/50 active:bg-muted"
      >
        <span className="min-w-0 flex-1">
          <span className="block font-medium [overflow-wrap:anywhere]">{name || user.email}</span>
          {name && <span className="block text-sm text-muted-foreground [overflow-wrap:anywhere]">{user.email}</span>}
          {(rollen.length > 0 || user.isLockedOut) && (
            <span className="mt-1 flex flex-wrap gap-1">
              {rollen.map((rolle) => (
                <Badge key={rolle} variant="secondary">
                  {rolle}
                </Badge>
              ))}
              {user.isLockedOut && <Badge variant="destructive">{t("Gesperrt")}</Badge>}
            </span>
          )}
        </span>
        <ChevronDown
          className={cn("size-5 shrink-0 text-muted-foreground transition-transform", offen && "rotate-180")}
          aria-hidden
        />
      </button>

      {offen && (
        <div className="flex flex-col gap-3 border-t px-4 py-3">
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <Button variant="outline" className="h-11" onClick={() => onPasswortForm(!passwortOffen)}>
              <KeyRound className="size-4" />
              {t("Passwort setzen")}
            </Button>
            <Button variant="outline" className="h-11" onClick={onLock}>
              {user.isLockedOut ? <Unlock className="size-4" /> : <Lock className="size-4" />}
              {user.isLockedOut ? t("Entsperren") : t("Sperren")}
            </Button>
            <Button variant="destructive" className="h-11" onClick={onDelete}>
              <Trash2 className="size-4" />
              {t("Löschen")}
            </Button>
          </div>

          {passwortOffen && (
            <div className="flex flex-col gap-2">
              <Label htmlFor={`pw-${user.id}`} className="text-xs leading-snug [overflow-wrap:anywhere]">
                {t("Neues Passwort für {email}", { email: user.email })}
              </Label>
              <Input
                id={`pw-${user.id}`}
                type="text"
                autoComplete="off"
                value={newPassword}
                onChange={(e) => onPasswortAendern(e.target.value)}
                placeholder={t("Mindestens 8 Zeichen")}
                className="h-11"
              />
              <div className="flex flex-wrap gap-2">
                <Button className="h-11 flex-1" disabled={settingPassword} onClick={onPasswortSetzen}>
                  {settingPassword ? t("Wird gesetzt…") : t("Passwort setzen")}
                </Button>
                <Button variant="ghost" className="h-11" onClick={() => onPasswortForm(false)}>
                  {t("Abbrechen")}
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                {t("Das Passwort wird im Klartext angezeigt, damit du es dem Nutzer sicher mitteilen kannst. Nach dem Setzen wird es nicht erneut angezeigt.")}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
