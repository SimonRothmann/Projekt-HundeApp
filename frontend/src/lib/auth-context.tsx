"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, ApiError, REFRESH_KEY, TOKEN_KEY, USER_KEY } from "@/lib/api";
import { alleSicherungenLoeschen } from "@/lib/aufzeichnung-sicherung";
import { loescheGesehen } from "@/lib/feedback-gesehen";
import { abrufFaellig } from "@/lib/glocke";
import { leseCacheLeeren } from "@/lib/read-cache";
import { loescheStartPo } from "@/lib/start-po";
import type { AuthResponse } from "@/lib/types";

type AuthUser = Pick<AuthResponse, "userId" | "email" | "firstName" | "lastName" | "roles">;

type AuthContextValue = {
  user: AuthUser | null;
  isLoading: boolean;
  // null = noch nicht ermittelt (z.B. während des ersten Requests nach
  // Login). "Trainer-Sein" ist bewusst rein datengetrieben (mind. eine
  // geleitete Gruppe oder Vereins-Trainer-Zuweisung, siehe TODO.md
  // "Rollenswitch") statt über eine eigene Identity-Rolle abgebildet.
  isTrainer: boolean | null;
  unreadNotificationCount: number;
  refreshUnreadNotificationCount: () => void;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<void>;
  updateUser: (patch: Partial<Pick<AuthUser, "firstName" | "lastName" | "email">>) => void;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

/** Wer sich zuletzt auf diesem Gerät angemeldet hat - nur die Id, nichts sonst. */
const LETZTE_PERSON_KEY = "dogity_letzte_person";

function letztePerson(): string | null {
  try {
    return window.localStorage.getItem(LETZTE_PERSON_KEY);
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isTrainer, setIsTrainer] = useState<boolean | null>(null);
  const [unreadNotificationCount, setUnreadNotificationCount] = useState(0);
  const [refreshTick, setRefreshTick] = useState(0);

  useEffect(() => {
    const storedUser = window.localStorage.getItem(USER_KEY);
    const storedToken = window.localStorage.getItem(TOKEN_KEY);
    if (storedUser && storedToken) {
      // Einmaliges Auslesen von localStorage beim Mount (externe Quelle) - keine
      // Render-Kaskade, da isLoading im selben Effect-Durchlauf gesetzt wird.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setUser(JSON.parse(storedUser));
    }
    setIsLoading(false);
  }, []);

  useEffect(() => {
    // Reset bei Logout passiert direkt in logout(), nicht hier - sonst
    // synchrones setState im Effect-Body (siehe react-hooks/set-state-in-effect).
    if (!user) return;

    let cancelled = false;
    api
      .get<{ isTrainer: boolean }>("/api/groups/my-trainer-status")
      .then((res) => {
        if (!cancelled) setIsTrainer(res.isTrainer);
      })
      .catch(() => {
        if (!cancelled) setIsTrainer(false);
      });
    return () => {
      cancelled = true;
    };
  }, [user]);

  useEffect(() => {
    if (!user) return;

    let cancelled = false;
    let letzterAbruf = 0;
    function fetchUnreadCount() {
      letzterAbruf = Date.now();
      api
        .get<number>("/api/notifications/unread-count")
        .then((count) => {
          if (!cancelled) setUnreadNotificationCount(count);
        })
        .catch(() => {
          // Stiller Fehlschlag - die Glocke ist nur ein Komfort-Hinweis.
        });
    }

    fetchUnreadCount();
    // Kein WebSocket/SignalR (siehe Plan) - einfaches Polling reicht für die
    // Vereinsgröße dieser App, analog zum isTrainer-Abruf-Muster oben.
    const interval = setInterval(fetchUnreadCount, 60_000);

    // Zusätzlich beim Zurückkehren in die App: Im Hintergrund bremst der
    // Browser den Takt, und die Glocke zeigte sonst noch den alten Stand.
    // Höchstens alle 10 s - der Takt oben zählt als Abruf mit (siehe glocke.ts).
    function beiSichtbarwerden() {
      if (document.visibilityState === "visible" && abrufFaellig(letzterAbruf, Date.now())) fetchUnreadCount();
    }
    document.addEventListener("visibilitychange", beiSichtbarwerden);
    return () => {
      cancelled = true;
      clearInterval(interval);
      document.removeEventListener("visibilitychange", beiSichtbarwerden);
    };
  }, [user, refreshTick]);

  function refreshUnreadNotificationCount() {
    setRefreshTick((t) => t + 1);
  }

  function updateUser(patch: Partial<Pick<AuthUser, "firstName" | "lastName" | "email">>) {
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, ...patch };
      window.localStorage.setItem(USER_KEY, JSON.stringify(next));
      return next;
    });
  }

  async function persist(response: AuthResponse) {
    // Meldet sich jemand anderes an als zuletzt, geht der Zwischenspeicher
    // der vorigen Person vorher weg - auch wenn sie sich nie abgemeldet hat,
    // sondern ihre Sitzung nur abgelaufen ist (dann räumt kein logout() auf).
    // Abgewartet, bevor die ersten Seiten den Speicher lesen.
    if (letztePerson() !== response.userId) await leseCacheLeeren();
    try {
      window.localStorage.setItem(LETZTE_PERSON_KEY, response.userId);
    } catch {
      // Ohne Merker wird beim nächsten Anmelden eben einmal zu viel geleert.
    }

    const authUser: AuthUser = {
      userId: response.userId,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      roles: response.roles,
    };
    window.localStorage.setItem(TOKEN_KEY, response.token);
    window.localStorage.setItem(REFRESH_KEY, response.refreshToken);
    window.localStorage.setItem(USER_KEY, JSON.stringify(authUser));
    setUser(authUser);
  }

  async function login(email: string, password: string) {
    const response = await api.post<AuthResponse>("/api/auth/login", { email, password });
    await persist(response);
  }

  async function register(email: string, password: string, firstName: string, lastName: string) {
    const response = await api.post<AuthResponse>("/api/auth/register", {
      email,
      password,
      firstName,
      lastName,
    });
    await persist(response);
  }

  function logout() {
    // Refresh-Token serverseitig widerrufen (nur dieses Gerät), damit ein
    // evtl. noch kopierter Token nach dem Logout wertlos ist. Fire-and-forget:
    // der lokale Logout darf nicht an einem Netzwerkfehler hängen.
    const refreshToken = window.localStorage.getItem(REFRESH_KEY);
    if (refreshToken) {
      api.post("/api/auth/logout", { refreshToken }).catch(() => {
        // Offline/Fehler: lokaler Logout genügt, der Token läuft ohnehin ab.
      });
    }
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(USER_KEY);
    window.localStorage.removeItem(REFRESH_KEY);
    alleSicherungenLoeschen();
    // Die gemerkte Prüfung vom Start gehört zur Person, nicht zum Gerät.
    loescheStartPo();
    // Ebenso der Merker, welches Trainer-Feedback schon gesehen wurde.
    loescheGesehen();
    // Hundeliste, Statistik, Tagebücher, Hundebilder - nichts davon bleibt
    // für die nächste Person am Gerät stehen. Noch nicht übertragene
    // Einträge bleiben dagegen: Sie gehören der Person (siehe
    // offline-queue.ts) und gehen hinaus, sobald sie sich wieder anmeldet.
    void leseCacheLeeren();
    setUser(null);
    setIsTrainer(null);
    setUnreadNotificationCount(0);
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isTrainer,
        unreadNotificationCount,
        refreshUnreadNotificationCount,
        login,
        register,
        updateUser,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth muss innerhalb von <AuthProvider> verwendet werden.");
  return ctx;
}

export { ApiError };
