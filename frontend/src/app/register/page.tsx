"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth, ApiError } from "@/lib/auth-context";
import { beitrittMitMeldung, einladungscodeAus } from "@/lib/einladung";
import { merkeStartPo, poSlugAus } from "@/lib/start-po";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PawPrint } from "lucide-react";
import { AuthBackLink } from "@/components/auth-back-link";
import { RechtlicheLinks } from "@/components/rechtliche-links";

import { useT } from "@/lib/i18n";
/**
 * Formular samt Verweisen. Eigene Komponente, weil useSearchParams eine
 * Suspense-Grenze braucht - sonst ließe sich die Seite nicht vorab bauen.
 */
function RegisterForm() {
  const t = useT();
  const { register } = useAuth();
  const router = useRouter();
  const suchparameter = useSearchParams();
  // Beide Werte kommen aus der Adresszeile und werden nur in der erwarteten
  // Form übernommen: der Einladungscode eines Vereins (/v/{code}) und das
  // Kürzel einer Prüfungsordnung von der öffentlichen Seite. Was nicht passt,
  // wird ignoriert - nichts davon wird je Teil einer Weiterleitung.
  const einladung = einladungscodeAus(suchparameter.get("einladung"));
  const pruefungsordnung = poSlugAus(suchparameter.get("po"));
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await register(email, password, firstName, lastName);
      // Erst nach dem Anlegen des Kontos: Bricht die Registrierung ab, soll
      // nichts vom Wunsch hängen bleiben.
      if (pruefungsordnung) merkeStartPo(pruefungsordnung);
      // Die Anfrage an den Verein hält das neue Konto nicht auf, wenn sie
      // scheitert - der Fehler wird gemeldet, dann geht es normal weiter.
      if (einladung && (await beitrittMitMeldung(einladung, t))) {
        router.push("/clubs");
        return;
      }
      router.push("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Registrierung fehlgeschlagen.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
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
          <Label htmlFor="password">Passwort</Label>
          <Input
            id="password"
            type="password"
            autoComplete="new-password"
            required
            minLength={8}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button type="submit" className="h-11" disabled={isSubmitting}>
          {isSubmitting ? t("Wird erstellt…") : "Registrieren"}
        </Button>
      </form>
      {/* Art. 13 DSGVO verlangt die Information zum Zeitpunkt der
          Erhebung - also hier, nicht erst irgendwo in der Fußzeile. */}
      <p className="mt-4 text-center text-xs text-muted-foreground">
        Mit dem Anlegen eines Kontos bestätigst du, die{" "}
        <Link href="/datenschutz" className="text-primary-text underline-offset-4 hover:underline">
          Datenschutzerklärung
        </Link>{" "}
        gelesen zu haben.
      </p>
      <p className="mt-4 text-center text-sm text-muted-foreground">
        Bereits ein Konto?{" "}
        <Link
          href={einladung ? `/login?einladung=${encodeURIComponent(einladung)}` : "/login"}
          className="text-primary-text underline-offset-4 hover:underline"
        >
          Anmelden
        </Link>
      </p>
    </>
  );
}

export default function RegisterPage() {
  const t = useT();
  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm flex-col gap-3">
        <AuthBackLink />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            <CardTitle className="text-xl">Konto erstellen</CardTitle>
            <CardDescription>{t("Starte dein Trainingstagebuch")}</CardDescription>
          </CardHeader>
          <CardContent>
            <Suspense>
              <RegisterForm />
            </Suspense>
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}
