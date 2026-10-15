import { api, ApiError } from "@/lib/api";

/**
 * Push-Benachrichtigungen (Web Push) auf diesem Gerät.
 *
 * Der obere Teil dieser Datei ist reine Logik ohne Zugriff auf den Browser,
 * damit sie sich testen lässt (push.test.ts): wann was angezeigt wird, und die
 * Umrechnung des Schlüssels. Darunter stehen die Browser-Aufrufe.
 *
 * iOS/iPadOS kann Web Push nur für Web-Apps, die zum Home-Bildschirm
 * hinzugefügt sind (ab 16.4, auch in der EU). Im normalen Safari-Tab fehlt
 * sogar das PushManager-Objekt - "nicht unterstützt" wäre dort eine falsche
 * Auskunft, denn mit dem Home-Bildschirm geht es. Deshalb wird dieser Fall VOR
 * der Unterstützung geprüft.
 */

export type PushZustand =
  /** Der Server hat Push nicht eingerichtet (kein öffentlicher Schlüssel): nichts anzeigen. */
  | "ausgeblendet"
  /** Der Browser kann kein Web Push (oder es ist keine App-Hülle installiert, z. B. im Entwicklungsmodus). */
  | "nicht-unterstuetzt"
  /** iPhone/iPad im Browser: erst zum Home-Bildschirm hinzufügen. */
  | "ios-ohne-home-bildschirm"
  /** Die Erlaubnis ist verweigert; nur die Systemeinstellungen helfen. */
  | "verweigert"
  /** Möglich, aber auf diesem Gerät nicht eingeschaltet. */
  | "aus"
  /** Eingeschaltet. */
  | "an";

export type PushUmgebung = {
  /** Öffentlicher VAPID-Schlüssel vom Server; null = Push dort nicht eingerichtet. */
  publicKey: string | null;
  hatServiceWorker: boolean;
  hatPushManager: boolean;
  hatNotification: boolean;
  /** iPhone, iPad oder iPod (auch ein iPad, das sich als Mac meldet). */
  istApple: boolean;
  /** Als App vom Home-Bildschirm gestartet. */
  istHomeBildschirm: boolean;
  /** `Notification.permission`; null, wenn es das Objekt nicht gibt. */
  erlaubnis: NotificationPermission | null;
  /** Ob dieses Gerät schon eine Push-Anmeldung hat. */
  abonniert: boolean;
};

export function bestimmePushZustand(u: PushUmgebung): PushZustand {
  if (!u.publicKey) return "ausgeblendet";
  if (u.istApple && !u.istHomeBildschirm) return "ios-ohne-home-bildschirm";
  if (!u.hatServiceWorker || !u.hatPushManager || !u.hatNotification) return "nicht-unterstuetzt";
  if (u.erlaubnis === "denied") return "verweigert";
  // Angemeldet UND erlaubt: Eine Anmeldung ohne Erlaubnis (zurückgenommen, aber
  // noch nicht bereinigt) liefert nichts mehr und zählt als aus.
  return u.abonniert && u.erlaubnis === "granted" ? "an" : "aus";
}

/**
 * Erkennt Apple-Geräte, auch ein iPad, das sich seit iPadOS 13 als Mac ausgibt
 * (dann verrät nur die Touch-Fähigkeit, dass es keiner ist).
 */
export function istAppleGeraet(userAgent: string, platform: string, maxTouchPoints: number): boolean {
  if (/iPhone|iPad|iPod/.test(userAgent)) return true;
  return platform === "MacIntel" && maxTouchPoints > 1;
}

/** VAPID-Schlüssel (base64url) in die Bytes, die `pushManager.subscribe` verlangt. */
export function urlBase64ToUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const mitPadding = base64Url + "=".repeat((4 - (base64Url.length % 4)) % 4);
  const base64 = mitPadding.replace(/-/g, "+").replace(/_/g, "/");
  const binaer = atob(base64);
  const bytes = new Uint8Array(new ArrayBuffer(binaer.length));
  for (let i = 0; i < binaer.length; i++) bytes[i] = binaer.charCodeAt(i);
  return bytes;
}

/**
 * Ob eine vorhandene Browser-Anmeldung zum aktuellen VAPID-Schlüssel des Servers
 * gehört. Bei einem anderen Schlüssel (Wechsel zwischen Test und Prod,
 * Schlüsseltausch) nimmt der Push-Dienst die Nachrichten nicht an (403) - die
 * Anmeldung muss dann neu erzeugt werden.
 */
export function gehoertZuSchluessel(angemeldetMit: ArrayBuffer | null | undefined, aktuell: Uint8Array): boolean {
  if (!angemeldetMit) return false;
  const bisher = new Uint8Array(angemeldetMit);
  return bisher.length === aktuell.length && bisher.every((b, i) => b === aktuell[i]);
}

// ---------------------------------------------------------------------------
// Ab hier: Browser. Jeder Zugriff darf scheitern (privater Modus, ältere
// Browser) und lässt die App ohne Push weiterlaufen.

/** Merkt sich die Adresse dieses Geräts, damit das Abmelden sie ohne Rückfrage an den Browser kennt. */
const ENDPOINT_KEY = "dogity_push_endpoint";

function merkeEndpoint(endpoint: string | null) {
  try {
    if (endpoint) window.localStorage.setItem(ENDPOINT_KEY, endpoint);
    else window.localStorage.removeItem(ENDPOINT_KEY);
  } catch {
    // Ohne Merker bleibt das Abmelden beim Ausloggen unvollständig (siehe pushBeimAbmeldenEntfernen).
  }
}

function gemerkterEndpoint(): string | null {
  try {
    return window.localStorage.getItem(ENDPOINT_KEY);
  } catch {
    return null;
  }
}

/** Wie lange auf einen Service Worker gewartet wird, bevor das Gerät als "ohne" gilt. */
const SERVICE_WORKER_WARTEZEIT_MS = 2500;

/**
 * Der Service Worker dieser App. `ready` löst nie auf, wenn keiner registriert
 * ist (im Entwicklungsmodus wird er bewusst entfernt, siehe pwa-register.tsx) -
 * deshalb nach kurzer Wartezeit aufgeben statt ewig zu warten.
 */
async function serviceWorkerRegistrierung(): Promise<ServiceWorkerRegistration | null> {
  if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return null;
  try {
    return await Promise.race([
      navigator.serviceWorker.ready,
      new Promise<null>((aufgeben) => setTimeout(() => aufgeben(null), SERVICE_WORKER_WARTEZEIT_MS)),
    ]);
  } catch {
    return null;
  }
}

function alsHomeBildschirmApp(): boolean {
  try {
    if (window.matchMedia("(display-mode: standalone)").matches) return true;
  } catch {
    // matchMedia nicht verfügbar - weiter mit der iOS-eigenen Angabe.
  }
  return (navigator as Navigator & { standalone?: boolean }).standalone === true;
}

/** Liest, was dieses Gerät kann und ob es schon angemeldet ist. */
export async function lesePushUmgebung(publicKey: string | null): Promise<PushUmgebung> {
  const registrierung = await serviceWorkerRegistrierung();
  let abonniert = false;
  try {
    abonniert = !!(await registrierung?.pushManager?.getSubscription());
  } catch {
    // Dann gilt es als nicht angemeldet.
  }
  return {
    publicKey,
    hatServiceWorker: registrierung !== null,
    hatPushManager: typeof window !== "undefined" && "PushManager" in window,
    hatNotification: typeof window !== "undefined" && "Notification" in window,
    istApple: istAppleGeraet(navigator.userAgent, navigator.platform, navigator.maxTouchPoints ?? 0),
    istHomeBildschirm: alsHomeBildschirmApp(),
    erlaubnis: typeof Notification === "undefined" ? null : Notification.permission,
    abonniert,
  };
}

/**
 * Schaltet Push auf diesem Gerät ein.
 *
 * MUSS direkt aus dem Tipp aufgerufen werden: iOS (und andere Browser) zeigen
 * die Frage nach der Erlaubnis nur nach einer Nutzergeste. Deshalb steht
 * `requestPermission` ganz vorn - vor jedem await auf etwas anderes.
 *
 * @returns "erteilt", "verweigert" oder "fehler" (Anmeldung oder Server schlug fehl).
 */
export async function schaltePushEin(publicKey: string): Promise<"erteilt" | "verweigert" | "fehler"> {
  try {
    const erlaubnis = await Notification.requestPermission();
    if (erlaubnis !== "granted") return "verweigert";

    const registrierung = await serviceWorkerRegistrierung();
    if (!registrierung) return "fehler";

    const serverSchluessel = urlBase64ToUint8Array(publicKey);
    let abo = await registrierung.pushManager.getSubscription();
    // Eine Anmeldung mit anderem Schlüssel taugt nicht (der Push-Dienst lehnt
    // sie ab, ohne dass die Oberfläche es merkt): lösen und neu anmelden.
    if (abo && !gehoertZuSchluessel(abo.options.applicationServerKey, serverSchluessel)) {
      // Auch den Eintrag beim Server entfernen, sonst bliebe er als Leiche liegen.
      await api.delete("/api/push/subscriptions", { endpoint: abo.endpoint }).catch(() => {});
      await abo.unsubscribe();
      abo = null;
    }
    abo ??= await registrierung.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: serverSchluessel,
    });

    // toJSON liefert genau die Form, die der Server erwartet: { endpoint, keys: { p256dh, auth } }.
    await api.post("/api/push/subscriptions", abo.toJSON());
    merkeEndpoint(abo.endpoint);
    return "erteilt";
  } catch {
    return "fehler";
  }
}

/**
 * Schaltet Push auf diesem Gerät aus: erst beim Server abmelden, dann beim Browser.
 * @returns false, wenn der Server das Gerät nicht abmelden konnte (lokal ist es trotzdem aus).
 */
export async function schaltePushAus(): Promise<boolean> {
  let serverOk = true;
  try {
    const registrierung = await serviceWorkerRegistrierung();
    const abo = await registrierung?.pushManager?.getSubscription();
    if (abo) {
      try {
        await api.delete("/api/push/subscriptions", { endpoint: abo.endpoint });
      } catch (err) {
        // 404: Der Server kennt das Gerät schon nicht mehr (z. B. nach einer
        // Rückmeldung 410 des Push-Dienstes) - das Ziel ist erreicht.
        serverOk = err instanceof ApiError && err.status === 404;
      }
      await abo.unsubscribe();
    }
  } catch {
    serverOk = false;
  }
  merkeEndpoint(null);
  return serverOk;
}

/** Nur die Anmeldung im Browser lösen (ohne Server) - für den Wechsel der Person am Gerät. */
export async function pushLokalLoesen(): Promise<void> {
  merkeEndpoint(null);
  try {
    const registrierung = await serviceWorkerRegistrierung();
    await (await registrierung?.pushManager?.getSubscription())?.unsubscribe();
  } catch {
    // Nichts zu lösen.
  }
}

/**
 * Beim Abmelden: Das Gerät gehört danach niemandem mehr und bekommt keine
 * Nachrichten der Person mehr.
 *
 * Synchron gestartet, weil logout() gleich danach den Anmelde-Token löscht -
 * die DELETE-Anfrage liest ihn im selben Atemzug. Deshalb die gemerkte Adresse
 * statt einer Rückfrage an den Browser (die wäre asynchron). Fehlt der Merker,
 * bleibt die Anmeldung auf dem Server bestehen, bis der Push-Dienst das Gerät
 * als ungültig meldet - dann hat der Browser sie aber schon gelöst.
 */
export function pushBeimAbmeldenEntfernen(): void {
  const endpoint = gemerkterEndpoint();
  if (endpoint) {
    api.delete("/api/push/subscriptions", { endpoint }).catch(() => {
      // Offline: Die Anmeldung im Browser wird trotzdem gelöst (unten).
    });
  }
  void pushLokalLoesen();
}

/**
 * Setzt die Zahl am App-Symbol (Home-Bildschirm, Dock). 0 entfernt sie. Wird
 * nicht überall unterstützt (iOS ab 16.4 nur für installierte Web-Apps) - ein
 * Fehlschlag ist kein Fehler.
 */
export function setzeAppBadge(anzahl: number): void {
  try {
    const nav = navigator as Navigator & {
      setAppBadge?: (n: number) => Promise<void>;
      clearAppBadge?: () => Promise<void>;
    };
    const aufgabe = anzahl > 0 ? nav.setAppBadge?.(anzahl) : nav.clearAppBadge?.();
    aufgabe?.catch(() => {
      // Z. B. keine Erlaubnis für Benachrichtigungen - dann eben keine Zahl.
    });
  } catch {
    // Kein Badge-Support.
  }
}
