"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, ApiError, TOKEN_KEY, USER_KEY } from "@/lib/api";
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
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isTrainer, setIsTrainer] = useState<boolean | null>(null);

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

  function persist(response: AuthResponse) {
    const authUser: AuthUser = {
      userId: response.userId,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      roles: response.roles,
    };
    window.localStorage.setItem(TOKEN_KEY, response.token);
    window.localStorage.setItem(USER_KEY, JSON.stringify(authUser));
    setUser(authUser);
  }

  async function login(email: string, password: string) {
    const response = await api.post<AuthResponse>("/api/auth/login", { email, password });
    persist(response);
  }

  async function register(email: string, password: string, firstName: string, lastName: string) {
    const response = await api.post<AuthResponse>("/api/auth/register", {
      email,
      password,
      firstName,
      lastName,
    });
    persist(response);
  }

  function logout() {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(USER_KEY);
    setUser(null);
    setIsTrainer(null);
  }

  return (
    <AuthContext.Provider value={{ user, isLoading, isTrainer, login, register, logout }}>
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
