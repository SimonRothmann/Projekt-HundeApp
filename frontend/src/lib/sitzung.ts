import { ABGEMELDET_KEY, REFRESH_KEY, TOKEN_KEY, USER_KEY, ZUGRIFF_BIS_KEY } from "@/lib/api";
import { pushLokalLoesen } from "@/lib/push";
import { leseCacheLeeren } from "@/lib/read-cache";
import type { AuthResponse } from "@/lib/types";

export type AuthUser = Pick<AuthResponse, "userId" | "email" | "firstName" | "lastName" | "roles">;

/** Wer sich zuletzt auf diesem Gerät angemeldet hat - nur die Id, nichts sonst. */
const LETZTE_PERSON_KEY = "dogity_letzte_person";

function letztePerson(): string | null {
  try {
    return window.localStorage.getItem(LETZTE_PERSON_KEY);
  } catch {
    return null;
  }
}

/**
 * Hält eine neue Anmeldung auf dem Gerät fest (Nutzer, Ablaufzeit; die Schlüssel setzt der Server als Cookies) und räumt auf, wenn jemand
 * anderes als zuletzt kommt. Herausgelöst aus dem AuthProvider, damit der Personenwechsel
 * ohne React testbar ist.
 */
export async function sitzungSpeichern(response: AuthResponse): Promise<AuthUser> {
  // Meldet sich jemand anderes an als zuletzt, geht der Zwischenspeicher
  // der vorigen Person vorher weg - auch wenn sie sich nie abgemeldet hat,
  // sondern ihre Sitzung nur abgelaufen ist (dann räumt kein logout() auf).
  // Abgewartet, bevor die ersten Seiten den Speicher lesen.
  if (letztePerson() !== response.userId) {
    await leseCacheLeeren();
    // Auch Push gehört der Person, nicht dem Gerät: War die Sitzung der
    // vorigen Person nur abgelaufen (kein logout()), hinge ihre Anmeldung noch
    // am Browser - und ihre Meldungen kämen auf dem Gerät der neuen Person an.
    void pushLokalLoesen();
  }
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
  window.localStorage.setItem(ZUGRIFF_BIS_KEY, response.accessExpiresAt);
  window.localStorage.setItem(USER_KEY, JSON.stringify(authUser));
  // Die neue Anmeldung gilt: ein noch offenes Abmelden von vorhin darf sie nicht beim nächsten Start beenden,
  // und Schlüssel aus der Zeit vor den Cookies sind überholt.
  window.localStorage.removeItem(ABGEMELDET_KEY);
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(REFRESH_KEY);
  return authUser;
}
