"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth-context";
import { useProfil } from "@/lib/use-profil";
import { useT } from "@/lib/i18n";
import { ListenGruppe, ListenZeile } from "@/components/profile/einstellungs-liste";
import { EmailSheet, NameSheet, PasswortSheet } from "@/components/profile/konto-sheets";

type Offen = "name" | "email" | "passwort" | null;

/** Name, E-Mail und Passwort: je Zeile ein Sheet mit dem Formular. */
export default function KontoPage() {
  const t = useT();
  const { user } = useAuth();
  const { profil, setProfil } = useProfil();
  const [offen, setOffen] = useState<Offen>(null);

  if (!user) return null;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Konto")}</h1>

      <ListenGruppe>
        {/* Das Namens-Sheet trägt auch den Avatar und braucht dessen Stand
            vom Server; bis der da ist, bliebe das Feld leer und ein
            Speichern löschte den Avatar. */}
        <ListenZeile
          titel={t("Name")}
          vorschau={`${user.firstName} ${user.lastName}`}
          onClick={() => profil && setOffen("name")}
        />
        <ListenZeile titel={t("E-Mail")} vorschau={user.email} onClick={() => setOffen("email")} />
        <ListenZeile titel={t("Passwort")} vorschau="••••••••" onClick={() => setOffen("passwort")} />
      </ListenGruppe>

      {profil && (
        <NameSheet
          offen={offen === "name"}
          onSchliessen={() => setOffen(null)}
          profil={profil}
          onGespeichert={setProfil}
        />
      )}
      <EmailSheet offen={offen === "email"} onSchliessen={() => setOffen(null)} />
      <PasswortSheet offen={offen === "passwort"} onSchliessen={() => setOffen(null)} />
    </div>
  );
}
