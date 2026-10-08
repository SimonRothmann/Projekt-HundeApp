"use client";

import { useT } from "@/lib/i18n";
import { LetzteNeuerung } from "@/components/letzte-neuerung";
import { NeuerungenGesehen } from "@/components/neuerungen-gesehen";
import { VersionStand } from "@/components/version-stand";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Die jüngste Fassung innerhalb der App (mit unterer Leiste). Die
 * vollständige Liste steht auf der öffentlichen Seite /neuerungen; der Link
 * dorthin sitzt in LetzteNeuerung.
 *
 * Öffnen gilt als Sehen: Der Punkt am Profil-Reiter und an der Zeile
 * "Neu in Dogity" verschwindet, sobald der Auszug im Bild ist.
 */
export default function ProfilNeuerungenPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Neu in Dogity")}</h1>
      <NeuerungenGesehen>
        <Card>
          <CardContent className="flex flex-col gap-4">
            <LetzteNeuerung />
            <VersionStand verlinkt={false} className="border-t pt-3" />
          </CardContent>
        </Card>
      </NeuerungenGesehen>
    </div>
  );
}
