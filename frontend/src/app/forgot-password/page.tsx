"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { AuthBackLink } from "@/components/auth-back-link";
import { RechtlicheLinks } from "@/components/rechtliche-links";

import { useT } from "@/lib/i18n";
export default function ForgotPasswordPage() {
  const t = useT();
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await api.post("/api/auth/forgot-password", { email });
      setSent(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t("Anfrage fehlgeschlagen."));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm flex-col gap-3">
        <AuthBackLink />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            <CardTitle className="text-xl">{t("Passwort vergessen")}</CardTitle>
            {!sent && (
              <CardDescription>{t("Wir informieren den Betreiber, der dein Passwort zurücksetzt")}</CardDescription>
            )}
          </CardHeader>
          <CardContent>
            {sent ? (
              // Ehrlicher Text für den Stand ohne Mailversand: Im Backend ist
              // nur LoggingEmailSender aktiv, es geht keine Mail raus - die
              // Admins bekommen stattdessen eine In-App-Benachrichtigung
              // (AuthController.ForgotPassword) und setzen das Passwort selbst
              // zurück. Weiterhin ohne Aussage, ob die Adresse existiert.
              // BEIM UMSCHALTEN AUF SmtpEmailSender (DependencyInjection.cs)
              // wieder zurückändern: Erfolgstext auf "Falls diese E-Mail-Adresse
              // registriert ist, wurde ein Link zum Zurücksetzen verschickt.
              // Bitte prüfe dein Postfach.", Untertitel oben auf "Wir schicken
              // dir einen Link zum Zurücksetzen" und Knopf auf "Link anfordern"
              // (alle drei in en.ts neu anlegen, die heutigen dort entfernen).
              <p className="text-sm text-muted-foreground">
                {t(
                  "Wenn die Adresse bei Dogity registriert ist, haben wir den Betreiber informiert. Er setzt dein Passwort zurück und meldet sich bei dir - das kann etwas dauern.",
                )}
              </p>
            ) : (
              <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="email">E-Mail</Label>
                  <Input
                    id="email"
                    type="email"
                    autoComplete="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </div>
                {error && <p className="text-sm text-destructive">{error}</p>}
                <Button type="submit" className="h-11" disabled={isSubmitting}>
                  {isSubmitting ? t("Wird gesendet…") : t("Zurücksetzen anfragen")}
                </Button>
              </form>
            )}
            <p className="mt-4 text-center text-sm text-muted-foreground">
              <Link href="/login" className="text-primary-text underline-offset-4 hover:underline">
{t("Zurück zur Anmeldung")}
              </Link>
            </p>
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}
