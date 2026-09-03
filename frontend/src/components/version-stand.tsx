"use client";

import Link from "next/link";
import { BUILD_COMMIT, BUILD_ZEIT, formatiereBuildZeit } from "@/lib/build-info";
import { AKTUELLE_VERSION } from "@/lib/versionshinweise";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Eine Zeile: welche Fassung läuft hier, seit wann, und aus welchem Commit
 * gebaut.
 *
 * Die drei Angaben stammen absichtlich aus zwei verschiedenen Quellen. Die
 * Fassungsnummer kommt aus den von Hand gepflegten Versionshinweisen und
 * sagt, was der Nutzer bekommen hat. Zeitpunkt und Commit entstehen beim
 * Bauen und sagen, was tatsächlich auf diesem Server liegt. Stimmt beides
 * nicht überein, ist genau das die Auskunft, die man braucht - eine hübsch
 * gerundete Einzelangabe hätte den Widerspruch verdeckt.
 *
 * Ohne Bauzeitpunkt (lokaler Build) bleibt die Zeile bei der Fassungsnummer.
 * Dann das Veröffentlichungsdatum als "Stand" auszugeben wäre bequem und
 * falsch: Es sagt, wann die Fassung geschrieben wurde, nicht wann dieser
 * Stand hier gebaut wurde.
 */
export function VersionStand({
  className,
  verlinkt = true,
}: {
  className?: string;
  verlinkt?: boolean;
}) {
  const t = useT();

  const fassung = t("Version {v}", { v: AKTUELLE_VERSION });

  return (
    <p className={cn("text-xs text-muted-foreground [overflow-wrap:anywhere]", className)}>
      {verlinkt ? (
        <Link href="/neuerungen" className="font-medium hover:text-foreground hover:underline">
          {fassung}
        </Link>
      ) : (
        <span className="font-medium">{fassung}</span>
      )}
      {BUILD_ZEIT && <> · {t("Stand {zeit}", { zeit: formatiereBuildZeit(BUILD_ZEIT) })}</>}
      {BUILD_COMMIT && (
        <>
          {` · ${t("Build")} `}
          <span className="font-mono">{BUILD_COMMIT}</span>
        </>
      )}
    </p>
  );
}
