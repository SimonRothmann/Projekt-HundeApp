"use client";

import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SupportButton } from "@/components/support-button";
import { LetzteNeuerung } from "@/components/letzte-neuerung";
import { useT } from "@/lib/i18n";
import { FAQ, FEATURES } from "@/components/marketing/startseite-daten";

/**
 * Der sichtbare Inhalt der Startseite.
 *
 * Eine Client-Komponente, damit er der gewählten Sprache folgt. Beim
 * Server-Rendern gilt die Vorgabe Deutsch (siehe SprachProvider): Das
 * ausgelieferte HTML - und damit, was die Suchmaschine liest - bleibt deutsch,
 * der Browser wechselt erst nach dem Hydrieren.
 */
export function StartseiteInhalt() {
  const t = useT();

  return (
    <main className="mx-auto w-full max-w-5xl flex-1 px-4">
      <section className="py-14 sm:py-20">
        <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-5xl">
          {t("Die Trainingstagebuch-App für den Hundesport")}
        </h1>
        <p className="mt-4 max-w-2xl text-base text-muted-foreground sm:text-lg">
          {t("Training festhalten, Fährten per GPS aufzeichnen und auswerten, Prüfungen vorbereiten. Für Hundesportler, Trainer und Vereine im Gebrauchshundesport – kostenlos und ohne Umweg über einen App-Store.")}
        </p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link href="/register" className={cn(buttonVariants({ size: "lg" }), "h-11 px-6 text-sm")}>
            {t("Kostenlos starten")}
          </Link>
          <Link
            href="/pruefungsordnungen"
            className={cn(buttonVariants({ variant: "outline", size: "lg" }), "h-11 px-6 text-sm")}
          >
            {t("Prüfungsordnungen ansehen")}
          </Link>
        </div>
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Was Dogity kann")}</h2>
        <div className="mt-8 grid gap-6 sm:grid-cols-2">
          {FEATURES.map(({ icon: Icon, title, text }) => (
            <article key={title} className="flex min-w-0 gap-3">
              <Icon className="mt-0.5 size-5 shrink-0 text-primary-text" aria-hidden />
              <div className="min-w-0">
                <h3 className="font-semibold">{t(title)}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{t(text)}</p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Für wen Dogity gedacht ist")}</h2>
        <div className="mt-6 grid gap-6 sm:grid-cols-3">
          <div className="min-w-0">
            <h3 className="font-semibold">{t("Hundesportler")}</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("Wer regelmäßig trainiert und auf eine Prüfung hinarbeitet, sieht nach Wochen sonst kaum noch, was wirklich sitzt. Dogity macht den Verlauf sichtbar.")}
            </p>
          </div>
          <div className="min-w-0">
            <h3 className="font-semibold">{t("Trainer")}</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("Gruppentrainings planen, Übungen zuweisen und den Mitgliedern gezielt Rückmeldung geben – auch zwischen den Trainingsterminen.")}
            </p>
          </div>
          <div className="min-w-0">
            <h3 className="font-semibold">{t("Vereine")}</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("Mitglieder, Trainingsgruppen und Termine an einer Stelle, statt verteilt über Aushang, Gruppenchat und Zettelwirtschaft.")}
            </p>
          </div>
        </div>
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Häufige Fragen")}</h2>
        <dl className="mt-6 flex flex-col gap-6">
          {FAQ.map((item) => (
            <div key={item.question} className="min-w-0">
              <dt className="font-semibold">{t(item.question)}</dt>
              <dd className="mt-1 text-sm text-muted-foreground">{t(item.answer)}</dd>
            </div>
          ))}
        </dl>
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Dogity unterstützen")}</h2>
        <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
          {t("Dogity entsteht in der Freizeit und läuft auf selbst bezahlten Servern – kostenlos, werbefrei und ohne bezahlte Zusatzfunktionen. Wer mag, kann die Entwicklung freiwillig über Ko-fi unterstützen.")}
        </p>
        <SupportButton className="mt-6" />
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Zuletzt geändert")}</h2>
        <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
          {t("An Dogity wird weitergearbeitet. Was zuletzt dazugekommen ist:")}
        </p>
        <div className="mt-6 rounded-lg border border-border/60 p-4 sm:p-5">
          <LetzteNeuerung />
        </div>
      </section>

      <section className="border-t border-border/60 py-12">
        <h2 className="text-2xl font-bold tracking-tight sm:text-3xl">{t("Loslegen")}</h2>
        <p className="mt-3 max-w-2xl text-sm text-muted-foreground">
          {t("Konto anlegen, Hund eintragen, erstes Training erfassen. Mehr braucht es nicht.")}
        </p>
        <Link
          href="/register"
          className={cn(buttonVariants({ size: "lg" }), "mt-6 inline-flex h-11 px-6 text-sm")}
        >
          {t("Kostenlos starten")}
        </Link>
      </section>
    </main>
  );
}
