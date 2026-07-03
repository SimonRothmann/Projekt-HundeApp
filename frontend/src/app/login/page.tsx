"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth, ApiError } from "@/lib/auth-context";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { EnvBadge, isTestEnv } from "@/components/env-badge";

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

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function doLogin(loginEmail: string, loginPassword: string) {
    setError(null);
    setIsSubmitting(true);
    try {
      await login(loginEmail, loginPassword);
      router.push("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Anmeldung fehlgeschlagen.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    await doLogin(email, password);
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="items-center text-center">
          <PawPrint className="size-8 text-primary" />
          <div className="flex items-center gap-2">
            <CardTitle className="text-xl">Bei Dogity anmelden</CardTitle>
            <EnvBadge />
          </div>
          <CardDescription>Trainingstagebuch & Hundesport-Plattform</CardDescription>
        </CardHeader>
        <CardContent>
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
                <Link href="/forgot-password" className="text-xs text-primary underline-offset-4 hover:underline">
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
            <Link href="/register" className="text-primary underline-offset-4 hover:underline">
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
        </CardContent>
      </Card>
    </main>
  );
}
