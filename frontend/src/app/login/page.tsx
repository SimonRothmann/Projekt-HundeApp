"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth, ApiError } from "@/lib/auth-context";
import { beitrittMitMeldung, einladungscodeAus } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { EnvBadge, isTestEnv } from "@/components/env-badge";
import { SupportButton } from "@/components/support-button";
import { AuthBackLink } from "@/components/auth-back-link";
import { RechtlicheLinks } from "@/components/rechtliche-links";

// Nur für die Test-/Dev-Datenbank (DemoDataSeeder, siehe TODO.md) - existiert
// nicht in Production. Aktiv wenn NEXT_PUBLIC_ENV_LABEL=TEST beim Build war;
// NODE_ENV ist im produktiven Next.js-Build immer "production" (auch für die
// Test-Umgebung), reicht als Diskriminator also nicht aus.
const DEMO_ACCOUNTS = [
  { label: "Admin", email: "admin@dogity.test" },
  { label: "Trainer", email: "trainer@dogity.test" },
  { label: "Mitglied 1", email: "mitglied1@dogity.test" },
  { label: "Mitglied 2", email: "mitglied2@dogity.test" },
  { label: "Interessent", email: "interessent@dogity.test" },
] as const;
const DEMO_PASSWORD = "Demo1234!";

/**
 * Formular samt Verweisen. Eigene Komponente, weil useSearchParams eine
 * Suspense-Grenze braucht - sonst ließe sich die Seite nicht vorab bauen.
 */
function LoginForm() {
  const t = useT();
  const { login } = useAuth();
  const router = useRouter();
  // Ein Einladungscode aus dem QR-Code des Vereins (/v/{code}). Nur in der
  // erwarteten Form, sonst wird er ignoriert - er landet nie in einer Adresse
  // oder einem Sprung, nur in genau einem API-Aufruf.
  const einladung = einladungscodeAus(useSearchParams().get("einladung"));
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function doLogin(loginEmail: string, loginPassword: string) {
    setError(null);
    setIsSubmitting(true);
    try {
      await login(loginEmail, loginPassword);
      await weiter();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Anmeldung fehlgeschlagen.");
    } finally {
      setIsSubmitting(false);
    }
  }

  /**
   * Nach der Anmeldung: mit Einladungslink zur Beitrittsanfrage, sonst wie
   * bisher zur Startseite. Scheitert die Anfrage, ist das eine Meldung wert,
   * hält die Anmeldung aber nicht auf - das Konto steht ja.
   */
  async function weiter() {
    if (einladung && (await beitrittMitMeldung(einladung, t))) {
      router.push("/clubs");
      return;
    }
    router.push("/dashboard");
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    await doLogin(email, password);
  }

  return (
    <>
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
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="password">Passwort</Label>
            <Link href="/forgot-password" className="text-xs text-primary-text underline-offset-4 hover:underline">
              Passwort vergessen?
            </Link>
          </div>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button type="submit" className="h-11" disabled={isSubmitting}>
          {isSubmitting ? "Anmelden…" : "Anmelden"}
        </Button>
      </form>
      <p className="mt-4 text-center text-sm text-muted-foreground">
        Noch kein Konto?{" "}
        <Link
          href={einladung ? `/register?einladung=${encodeURIComponent(einladung)}` : "/register"}
          className="text-primary-text underline-offset-4 hover:underline"
        >
          Registrieren
        </Link>
      </p>
      {isTestEnv && (
        <div className="mt-6 border-t pt-4">
          <p className="mb-2 text-center text-xs text-muted-foreground">
            Demo-Login (nur Test-Umgebung)
          </p>
          <div className="grid grid-cols-2 gap-2">
            {DEMO_ACCOUNTS.map((account) => (
              <Button
                key={account.email}
                type="button"
                variant="outline"
                size="sm"
                disabled={isSubmitting}
                onClick={() => doLogin(account.email, DEMO_PASSWORD)}
              >
                {account.label}
              </Button>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

export default function LoginPage() {
  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm flex-col gap-3">
        <AuthBackLink />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            <div className="flex items-center gap-2">
              <CardTitle className="text-xl">Bei Dogity anmelden</CardTitle>
              <EnvBadge />
            </div>
            <CardDescription>Trainingstagebuch & Hundesport-Plattform</CardDescription>
          </CardHeader>
          <CardContent>
            <Suspense>
              <LoginForm />
            </Suspense>
            <div className="mt-6 flex justify-center">
              <SupportButton />
            </div>
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}
