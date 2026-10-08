"use client";

import { useState, type FormEvent, type ReactNode } from "react";
import { toast } from "sonner";
import { api, ApiError, REFRESH_KEY } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import type { Profile } from "@/lib/types";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { PasswortHinweis } from "@/components/passwort-hinweis";

/**
 * Die drei Änderungen am Konto - je ein Sheet von unten, das beim Antippen
 * der Zeile aufgeht. Die Formulare stehen erst im Sheet, wenn es offen ist:
 * Beim nächsten Öffnen sind die Felder deshalb frisch, ohne dass jemand sie
 * von Hand zurücksetzen müsste (und ein eingetipptes Passwort bleibt nicht
 * im Speicher liegen).
 */
function KontoSheet({
  offen,
  onSchliessen,
  titel,
  beschreibung,
  children,
}: {
  offen: boolean;
  onSchliessen: () => void;
  titel: string;
  beschreibung?: string;
  children: ReactNode;
}) {
  return (
    <Sheet open={offen} onOpenChange={(naechster) => !naechster && onSchliessen()}>
      <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto pb-[env(safe-area-inset-bottom)]">
        <SheetHeader>
          <SheetTitle>{titel}</SheetTitle>
          {beschreibung && <SheetDescription>{beschreibung}</SheetDescription>}
        </SheetHeader>
        {children}
      </SheetContent>
    </Sheet>
  );
}

export function NameSheet({
  offen,
  onSchliessen,
  profil,
  onGespeichert,
}: {
  offen: boolean;
  onSchliessen: () => void;
  profil: Profile;
  onGespeichert: (profil: Profile) => void;
}) {
  const t = useT();
  return (
    <KontoSheet offen={offen} onSchliessen={onSchliessen} titel={t("Name & Avatar")}>
      <NameFormular profil={profil} onSchliessen={onSchliessen} onGespeichert={onGespeichert} />
    </KontoSheet>
  );
}

function NameFormular({
  profil,
  onSchliessen,
  onGespeichert,
}: {
  profil: Profile;
  onSchliessen: () => void;
  onGespeichert: (profil: Profile) => void;
}) {
  const t = useT();
  const { updateUser } = useAuth();
  const [firstName, setFirstName] = useState(profil.firstName);
  const [lastName, setLastName] = useState(profil.lastName);
  const [avatarUrl, setAvatarUrl] = useState(profil.avatarUrl ?? "");
  const [speichert, setSpeichert] = useState(false);

  const initials = `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    setSpeichert(true);
    try {
      await api.put("/api/profile", { firstName, lastName, avatarUrl: avatarUrl || null });
      updateUser({ firstName, lastName });
      onGespeichert({ ...profil, firstName, lastName, avatarUrl: avatarUrl || null });
      toast.success(t("Profil aktualisiert."));
      onSchliessen();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Speichern fehlgeschlagen."));
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={handleSave} className="flex flex-col gap-4 p-4 pt-0">
      <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2">
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="firstName">{t("Vorname")}</Label>
          <Input id="firstName" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
        </div>
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="lastName">{t("Nachname")}</Label>
          <Input id="lastName" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
        </div>
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="avatarUrl">{t("Avatar-URL (optional)")}</Label>
        <Input
          id="avatarUrl"
          type="url"
          placeholder="https://..."
          value={avatarUrl}
          onChange={(e) => setAvatarUrl(e.target.value)}
        />
        {avatarUrl && (
          <Avatar className="size-12">
            <AvatarImage src={avatarUrl} />
            <AvatarFallback>{initials}</AvatarFallback>
          </Avatar>
        )}
      </div>
      <Button type="submit" disabled={speichert} className="coarse:min-h-11">
        {speichert ? t("Speichert…") : t("Speichern")}
      </Button>
    </form>
  );
}

export function EmailSheet({ offen, onSchliessen }: { offen: boolean; onSchliessen: () => void }) {
  const t = useT();
  return (
    <KontoSheet offen={offen} onSchliessen={onSchliessen} titel={t("E-Mail ändern")}>
      <EmailFormular onSchliessen={onSchliessen} />
    </KontoSheet>
  );
}

function EmailFormular({ onSchliessen }: { onSchliessen: () => void }) {
  const t = useT();
  const { updateUser } = useAuth();
  const [newEmail, setNewEmail] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [speichert, setSpeichert] = useState(false);

  async function handleChangeEmail(e: FormEvent) {
    e.preventDefault();
    setSpeichert(true);
    try {
      await api.put("/api/profile/email", { newEmail, currentPassword: emailPassword });
      updateUser({ email: newEmail });
      toast.success(t("E-Mail-Adresse geändert."));
      onSchliessen();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ändern fehlgeschlagen."));
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={handleChangeEmail} className="flex flex-col gap-4 p-4 pt-0">
      <div className="flex flex-col gap-2">
        <Label htmlFor="newEmail">{t("Neue E-Mail")}</Label>
        <Input id="newEmail" type="email" required value={newEmail} onChange={(e) => setNewEmail(e.target.value)} />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="emailPassword">{t("Aktuelles Passwort")}</Label>
        <Input
          id="emailPassword"
          type="password"
          required
          value={emailPassword}
          onChange={(e) => setEmailPassword(e.target.value)}
        />
      </div>
      <Button type="submit" disabled={speichert} className="coarse:min-h-11">
        {speichert ? t("Ändert…") : t("Ändern")}
      </Button>
    </form>
  );
}

export function PasswortSheet({ offen, onSchliessen }: { offen: boolean; onSchliessen: () => void }) {
  const t = useT();
  return (
    <KontoSheet offen={offen} onSchliessen={onSchliessen} titel={t("Passwort ändern")}>
      <PasswortFormular onSchliessen={onSchliessen} />
    </KontoSheet>
  );
}

function PasswortFormular({ onSchliessen }: { onSchliessen: () => void }) {
  const t = useT();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [speichert, setSpeichert] = useState(false);

  async function handleChangePassword(e: FormEvent) {
    e.preventDefault();
    setSpeichert(true);
    try {
      // Der Refresh-Token dieses Geräts geht mit: Alle ANDEREN Sitzungen
      // enden mit dem Wechsel, diese bleibt bestehen.
      await api.put("/api/profile/password", {
        currentPassword,
        newPassword,
        refreshToken: window.localStorage.getItem(REFRESH_KEY),
      });
      toast.success(t("Passwort geändert. Andere Geräte wurden abgemeldet."));
      onSchliessen();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ändern fehlgeschlagen."));
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={handleChangePassword} className="flex flex-col gap-4 p-4 pt-0">
      <div className="flex flex-col gap-2">
        <Label htmlFor="currentPassword">{t("Aktuelles Passwort")}</Label>
        <Input
          id="currentPassword"
          type="password"
          required
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
        />
      </div>
      <div className="flex flex-col gap-2">
        <Label htmlFor="newPassword">{t("Neues Passwort")}</Label>
        <Input
          id="newPassword"
          type="password"
          required
          minLength={8}
          aria-describedby="newPassword-hinweis"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
        />
        <PasswortHinweis id="newPassword-hinweis" />
      </div>
      <Button type="submit" disabled={speichert} className="coarse:min-h-11">
        {speichert ? t("Ändert…") : t("Ändern")}
      </Button>
    </form>
  );
}
