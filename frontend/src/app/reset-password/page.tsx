"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { AuthBackLink } from "@/components/auth-back-link";

import { useT } from "@/lib/i18n";
function ResetPasswordForm() {
  const t = useT();
  const router = useRouter();
  const searchParams = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token") ?? "";

  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await api.post("/api/auth/reset-password", { email, token, newPassword });
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("Zurücksetzen fehlgeschlagen."));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!email || !token) {
    return <p className="text-sm text-destructive">{t("Link ist ungültig. Bitte fordere einen neuen Link an.")}</p>;
  }

  if (done) {
    return (
      <div className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">{t("Dein Passwort wurde geändert.")}</p>
        <Button className="h-11" onClick={() => router.push("/login")}>
          Zur Anmeldung
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="newPassword">{t("Neues Passwort")}</Label>
        <Input
          id="newPassword"
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
        />
      </div>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <Button type="submit" className="h-11" disabled={isSubmitting}>
        {isSubmitting ? t("Wird geändert…") : t("Passwort ändern")}
      </Button>
    </form>
  );
}

export default function ResetPasswordPage() {
  const t = useT();
  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm flex-col gap-3">
        <AuthBackLink />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary" />
            <CardTitle className="text-xl">{t("Neues Passwort setzen")}</CardTitle>
            <CardDescription>{t("Wähle ein neues Passwort für dein Konto")}</CardDescription>
          </CardHeader>
          <CardContent>
            <Suspense>
              <ResetPasswordForm />
            </Suspense>
            <p className="mt-4 text-center text-sm text-muted-foreground">
              <Link href="/login" className="text-primary underline-offset-4 hover:underline">
{t("Zurück zur Anmeldung")}
              </Link>
            </p>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
