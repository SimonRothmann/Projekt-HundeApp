"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api } from "@/lib/api";
import type { UserPreferences } from "@/lib/types";
import { useAuth } from "@/lib/auth-context";

/**
 * Die persönlichen Einstellungen an einer Stelle, damit nicht jede
 * Komponente sie einzeln holt (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
 *
 * Die Vorgabe ist bewusst "alles an": Solange die Einstellungen noch nicht
 * geladen sind, verhält sich die App wie bisher. Das ist wichtiger als es
 * klingt - andersherum würde bei jedem Seitenaufbau kurz die halbe App
 * fehlen und dann aufpoppen.
 */
type PreferencesValue = {
  preferences: UserPreferences;
  /** Ob ein Modul angezeigt werden soll. Unbekannte Module gelten als an. */
  moduleEnabled: (key: string) => boolean;
  /** Nach dem Speichern aufrufen, damit die Oberfläche sofort nachzieht. */
  reload: () => Promise<void>;
};

const VORGABE: UserPreferences = { locale: null, disabledModules: [], sportIds: [] };

const PreferencesContext = createContext<PreferencesValue | undefined>(undefined);

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [preferences, setPreferences] = useState<UserPreferences>(VORGABE);

  const reload = useCallback(async () => {
    try {
      setPreferences(await api.get<UserPreferences>("/api/preferences"));
    } catch {
      // Einstellungen sind Beiwerk: Fällt der Abruf aus (offline, Serverfehler),
      // bleibt es bei "alles an" statt bei einer halben App.
      setPreferences(VORGABE);
    }
  }, []);

  useEffect(() => {
    if (!user) {
      // Abmelden: zurück auf die Vorgabe, sonst behielte der nächste Nutzer
      // am selben Gerät die Modulauswahl seines Vorgängers.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPreferences(VORGABE);
      return;
    }
    // Initialer Datenabruf nach Anmeldung (externe Quelle: REST API).
    void reload();
  }, [user, reload]);

  const value = useMemo<PreferencesValue>(
    () => ({
      preferences,
      moduleEnabled: (key: string) => !preferences.disabledModules.includes(key),
      reload,
    }),
    [preferences, reload],
  );

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences(): PreferencesValue {
  const context = useContext(PreferencesContext);
  if (!context) throw new Error("usePreferences muss innerhalb von PreferencesProvider verwendet werden.");
  return context;
}
