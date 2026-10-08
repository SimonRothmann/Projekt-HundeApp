"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { listOwnQueuedRequests } from "@/lib/offline-queue";
import { useT } from "@/lib/i18n";
import { ListenGruppe, ListenZeile } from "@/components/profile/einstellungs-liste";

/** Die rote Textzeile "Abmelden" ganz unten im Profil. */
export function AbmeldenZeile() {
  const t = useT();
  const { logout } = useAuth();
  const router = useRouter();

  async function handleLogout() {
    // Offline Erfasstes bleibt beim Abmelden auf dem Gerät und geht hinaus,
    // sobald die Person sich wieder anmeldet - löschen würde Trainingsdaten
    // vernichten. Wer sich abmeldet, soll aber wissen, dass sie noch
    // unterwegs sind.
    const offen = await listOwnQueuedRequests().catch(() => []);
    if (
      offen.length > 0 &&
      !window.confirm(
        offen.length === 1
          ? t("Ein Eintrag ist noch nicht übertragen. Er bleibt auf diesem Gerät und wird gesendet, sobald du dich hier wieder anmeldest. Trotzdem abmelden?")
          : t("{anzahl} Einträge sind noch nicht übertragen. Sie bleiben auf diesem Gerät und werden gesendet, sobald du dich hier wieder anmeldest. Trotzdem abmelden?", {
              anzahl: offen.length,
            }),
      )
    )
      return;
    logout();
    router.push("/login");
  }

  return (
    <ListenGruppe>
      <ListenZeile titel={t("Abmelden")} onClick={() => void handleLogout()} gefahr pfeil={false} />
    </ListenGruppe>
  );
}
