"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth-context";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import type { Profile } from "@/lib/types";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { LogOut, ShieldCheck, Pencil } from "lucide-react";
import { toast } from "sonner";

export default function ProfilePage() {
  const { user, logout, updateUser } = useAuth();
  const router = useRouter();
  const [editing, setEditing] = useState(false);

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [savingProfile, setSavingProfile] = useState(false);

  const [newEmail, setNewEmail] = useState("");
  const [emailPassword, setEmailPassword] = useState("");
  const [savingEmail, setSavingEmail] = useState(false);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);

  useEffect(() => {
    if (!user) return;
    api
      .get<Profile>("/api/profile")
      .then((p) => {
        setFirstName(p.firstName);
        setLastName(p.lastName);
        setAvatarUrl(p.avatarUrl ?? "");
      })
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Profil konnte nicht geladen werden."));
  }, [user]);

  if (!user) return null;

  const initials = `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();

  function handleLogout() {
    logout();
    router.push("/login");
  }

  async function handleSaveProfile(e: FormEvent) {
    e.preventDefault();
    setSavingProfile(true);
    try {
      await api.put("/api/profile", { firstName, lastName, avatarUrl: avatarUrl || null });
      updateUser({ firstName, lastName });
      toast.success("Profil aktualisiert.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Speichern fehlgeschlagen.");
    } finally {
      setSavingProfile(false);
    }
  }

  async function handleChangeEmail(e: FormEvent) {
    e.preventDefault();
    setSavingEmail(true);
    try {
      await api.put("/api/profile/email", { newEmail, currentPassword: emailPassword });
      updateUser({ email: newEmail });
      toast.success("E-Mail-Adresse geändert.");
      setNewEmail("");
      setEmailPassword("");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ändern fehlgeschlagen.");
    } finally {
      setSavingEmail(false);
    }
  }

  async function handleChangePassword(e: FormEvent) {
    e.preventDefault();
    setSavingPassword(true);
    try {
      await api.put("/api/profile/password", { currentPassword, newPassword });
      toast.success("Passwort geändert.");
      setCurrentPassword("");
      setNewPassword("");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ändern fehlgeschlagen.");
    } finally {
      setSavingPassword(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">Profil</h1>

      <Card>
        <CardHeader className="flex-row items-center gap-4 space-y-0">
          <Avatar className="size-16">
            {avatarUrl && <AvatarImage src={avatarUrl} />}
            <AvatarFallback className="text-lg">{initials}</AvatarFallback>
          </Avatar>
          <div className="flex-1">
            <CardTitle>
              {user.firstName} {user.lastName}
            </CardTitle>
            <p className="text-sm text-muted-foreground">{user.email}</p>
          </div>
          {!editing && (
            <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
              <Pencil className="size-4" />
              Bearbeiten
            </Button>
          )}
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2">
            {user.roles.map((role) => (
              <Badge key={role} variant="secondary">
                {role}
              </Badge>
            ))}
          </div>
          {user.roles.includes("ADMIN") && (
            <Link
              href="/admin"
              className={`self-start md:hidden ${buttonVariants({ variant: "outline" })}`}
            >
              <ShieldCheck className="size-4" />
              Admin-Übersicht
            </Link>
          )}
          <Button variant="destructive" className="self-start" onClick={handleLogout}>
            <LogOut className="size-4" />
            Abmelden
          </Button>
        </CardContent>
      </Card>

      {editing && (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Name & Avatar</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSaveProfile} className="flex flex-col gap-4">
                <div className="grid grid-cols-2 gap-3">
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="firstName">Vorname</Label>
                    <Input id="firstName" required value={firstName} onChange={(e) => setFirstName(e.target.value)} />
                  </div>
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="lastName">Nachname</Label>
                    <Input id="lastName" required value={lastName} onChange={(e) => setLastName(e.target.value)} />
                  </div>
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="avatarUrl">Avatar-URL (optional)</Label>
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
                <div className="flex gap-2">
                  <Button type="submit" disabled={savingProfile}>
                    {savingProfile ? "Speichert…" : "Speichern"}
                  </Button>
                  <Button type="button" variant="ghost" onClick={() => setEditing(false)}>
                    Schließen
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">E-Mail ändern</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleChangeEmail} className="flex flex-col gap-4 sm:flex-row sm:items-end">
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="newEmail">Neue E-Mail</Label>
                  <Input id="newEmail" type="email" required value={newEmail} onChange={(e) => setNewEmail(e.target.value)} />
                </div>
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="emailPassword">Aktuelles Passwort</Label>
                  <Input
                    id="emailPassword"
                    type="password"
                    required
                    value={emailPassword}
                    onChange={(e) => setEmailPassword(e.target.value)}
                  />
                </div>
                <Button type="submit" disabled={savingEmail}>
                  {savingEmail ? "Ändert…" : "Ändern"}
                </Button>
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Passwort ändern</CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleChangePassword} className="flex flex-col gap-4 sm:flex-row sm:items-end">
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="currentPassword">Aktuelles Passwort</Label>
                  <Input
                    id="currentPassword"
                    type="password"
                    required
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                  />
                </div>
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="newPassword">Neues Passwort</Label>
                  <Input
                    id="newPassword"
                    type="password"
                    required
                    minLength={8}
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                  />
                </div>
                <Button type="submit" disabled={savingPassword}>
                  {savingPassword ? "Ändert…" : "Ändern"}
                </Button>
              </form>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
