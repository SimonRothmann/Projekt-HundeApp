"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { AuthKopfzeile } from "@/components/auth-back-link";
import { RechtlicheLinks } from "@/components/rechtliche-links";

import { useT } from "@/lib/i18n";
import { useSystemFeatures } from "@/lib/system-features";
export default function ForgotPasswordPage() {
  const t = useT();
  // Ob der Server Mails versendet. Solange das nicht feststeht (oder der Abruf
  // scheitert), gilt der ehrliche Text ohne Mail: Er verspricht nichts, was nicht läuft.
  const features = useSystemFeatures();
  const perMail = features?.email === true;
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
        <AuthKopfzeile />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            <CardTitle className="text-xl">{t("Passwort vergessen")}</CardTitle>
            {!sent && (
              <CardDescription>
                {perMail
                  ? t("Wir schicken dir einen Link zum Zurücksetzen per E-Mail")
                  : t("Wir informieren den Betreiber, der dein Passwort zurücksetzt")}
              </CardDescription>
            )}
          </CardHeader>
          <CardContent>
            {sent ? (
              // Zwei Fassungen, je nachdem, ob der Server Mails versendet
              // (GET /api/system/features). Ohne Mailversand sagt der Text
              // ehrlich, dass der Betreiber das Passwort zurücksetzt - die Admins
              // bekommen dann eine Benachrichtigung in der App
              // (AccountMailService). In beiden Fällen ohne Aussage, ob die
              // Adresse existiert; die Antwort des Servers ist immer gleich.
              <p className="text-sm text-muted-foreground">
                {perMail
                  ? t(
                      "Falls diese E-Mail-Adresse registriert ist, haben wir dir einen Link zum Zurücksetzen geschickt. Bitte prüfe dein Postfach - auch den Spam-Ordner.",
                    )
                  : t(
                      "Wenn die Adresse bei Dogity registriert ist, haben wir den Betreiber informiert. Er setzt dein Passwort zurück und meldet sich bei dir - das kann etwas dauern.",
                    )}
              </p>
            ) : (
              <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="email">{t("E-Mail")}</Label>
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
                  {isSubmitting ? t("Wird gesendet…") : perMail ? t("Link anfordern") : t("Zurücksetzen anfragen")}
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
