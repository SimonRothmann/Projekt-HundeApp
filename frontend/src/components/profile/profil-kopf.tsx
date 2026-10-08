"use client";

import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";

// Die Rollen kommen als Rohwerte vom Server; lesbar gemacht werden sie erst hier.
const ROLLEN_NAME: Record<string, string> = {
  USER: uebersetzbar("Mitglied"),
  TRAINER: uebersetzbar("Trainer:in"),
  ADMIN: uebersetzbar("Admin"),
};

/** Die oberste Zeile des Profils: wer bin ich - und der Weg zu den Kontodaten. */
export function ProfilKopf({ avatarUrl }: { avatarUrl: string | null }) {
  const t = useT();
  const { user } = useAuth();
  if (!user) return null;

  const initials = `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();

  return (
    <ListenGruppe>
      <Link
        href="/profile/konto"
        className="flex min-h-[3.25rem] items-center gap-4 px-4 py-4 transition-colors hover:bg-muted/50 active:bg-muted"
      >
        <Avatar className="size-16 shrink-0">
          {avatarUrl && <AvatarImage src={avatarUrl} />}
          <AvatarFallback className="text-lg">{initials}</AvatarFallback>
        </Avatar>
        <span className="min-w-0 flex-1">
          <span className="block text-base font-semibold break-words">
            {user.firstName} {user.lastName}
          </span>
          <span className="block text-sm break-all text-muted-foreground">{user.email}</span>
          <span className="mt-1.5 flex flex-wrap gap-1.5">
            {user.roles.map((role) => (
              <Badge key={role} variant="secondary">
                {t(ROLLEN_NAME[role] ?? role)}
              </Badge>
            ))}
          </span>
        </span>
        <ChevronRight className="size-5 shrink-0 text-muted-foreground" aria-hidden />
      </Link>
    </ListenGruppe>
  );
}
