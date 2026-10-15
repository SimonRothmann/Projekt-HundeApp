import { describe, expect, it } from "vitest";
import { bestimmePushZustand, gehoertZuSchluessel, istAppleGeraet, urlBase64ToUint8Array, type PushUmgebung } from "./push";

/** Ein Gerät, das alles kann und noch nichts eingeschaltet hat. */
function umgebung(abweichung: Partial<PushUmgebung> = {}): PushUmgebung {
  return {
    publicKey: "BPublicKey",
    hatServiceWorker: true,
    hatPushManager: true,
    hatNotification: true,
    istApple: false,
    istHomeBildschirm: false,
    erlaubnis: "default",
    abonniert: false,
    ...abweichung,
  };
}

describe("bestimmePushZustand", () => {
  it("blendet alles aus, wenn der Server Push nicht eingerichtet hat", () => {
    expect(bestimmePushZustand(umgebung({ publicKey: null }))).toBe("ausgeblendet");
    expect(bestimmePushZustand(umgebung({ publicKey: "" }))).toBe("ausgeblendet");
  });

  it("blendet auch dann aus, wenn das Gerät sonst nichts kann - der Server entscheidet zuerst", () => {
    expect(bestimmePushZustand(umgebung({ publicKey: null, hatPushManager: false, istApple: true }))).toBe("ausgeblendet");
  });

  it("bietet Einschalten an, wenn Push geht und noch nicht an ist", () => {
    expect(bestimmePushZustand(umgebung())).toBe("aus");
    expect(bestimmePushZustand(umgebung({ erlaubnis: "granted" }))).toBe("aus");
  });

  it("zeigt 'an' nur bei Anmeldung UND Erlaubnis", () => {
    expect(bestimmePushZustand(umgebung({ abonniert: true, erlaubnis: "granted" }))).toBe("an");
    // Erlaubnis im Gerät zurückgenommen, Anmeldung noch da: es kommt nichts mehr an.
    expect(bestimmePushZustand(umgebung({ abonniert: true, erlaubnis: "default" }))).toBe("aus");
  });

  it("erkennt die verweigerte Erlaubnis, auch mit alter Anmeldung", () => {
    expect(bestimmePushZustand(umgebung({ erlaubnis: "denied" }))).toBe("verweigert");
    expect(bestimmePushZustand(umgebung({ erlaubnis: "denied", abonniert: true }))).toBe("verweigert");
  });

  it("sagt auf dem iPhone im Browser, dass es der Home-Bildschirm sein muss - nicht 'nicht unterstützt'", () => {
    // Im Safari-Tab gibt es auf iOS weder PushManager noch Service-Worker-Push.
    const ios = umgebung({ istApple: true, istHomeBildschirm: false, hatPushManager: false, hatNotification: false });
    expect(bestimmePushZustand(ios)).toBe("ios-ohne-home-bildschirm");
  });

  it("bietet auf dem iPhone als Home-Bildschirm-App das Einschalten an", () => {
    expect(bestimmePushZustand(umgebung({ istApple: true, istHomeBildschirm: true }))).toBe("aus");
    expect(bestimmePushZustand(umgebung({ istApple: true, istHomeBildschirm: true, abonniert: true, erlaubnis: "granted" }))).toBe("an");
  });

  it("verlangt den Home-Bildschirm nur auf Apple-Geräten", () => {
    // Android/Desktop: Browser-Tab genügt.
    expect(bestimmePushZustand(umgebung({ istApple: false, istHomeBildschirm: false }))).toBe("aus");
  });

  it("meldet 'nicht unterstützt', wenn dem Browser etwas fehlt", () => {
    expect(bestimmePushZustand(umgebung({ hatServiceWorker: false }))).toBe("nicht-unterstuetzt");
    expect(bestimmePushZustand(umgebung({ hatPushManager: false }))).toBe("nicht-unterstuetzt");
    expect(bestimmePushZustand(umgebung({ hatNotification: false, erlaubnis: null }))).toBe("nicht-unterstuetzt");
  });
});

describe("istAppleGeraet", () => {
  it("erkennt iPhone und iPad am User-Agent", () => {
    expect(istAppleGeraet("Mozilla/5.0 (iPhone; CPU iPhone OS 18_4 like Mac OS X)", "iPhone", 5)).toBe(true);
    expect(istAppleGeraet("Mozilla/5.0 (iPad; CPU OS 18_4 like Mac OS X)", "iPad", 5)).toBe(true);
  });

  it("erkennt ein iPad, das sich als Mac meldet", () => {
    expect(istAppleGeraet("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", "MacIntel", 5)).toBe(true);
  });

  it("hält einen echten Mac nicht für ein iPad", () => {
    expect(istAppleGeraet("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", "MacIntel", 0)).toBe(false);
  });

  it("erkennt Android und Windows nicht als Apple", () => {
    expect(istAppleGeraet("Mozilla/5.0 (Linux; Android 14; Pixel 8)", "Linux armv81", 5)).toBe(false);
    expect(istAppleGeraet("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", "Win32", 0)).toBe(false);
  });
});

describe("urlBase64ToUint8Array", () => {
  it("wandelt einen VAPID-Schlüssel in seine 65 Byte", () => {
    // Ein unkomprimierter P-256-Punkt: 0x04 + X (32) + Y (32), base64url ohne Padding = 87 Zeichen.
    const bytes = new Uint8Array(65);
    bytes[0] = 4;
    for (let i = 1; i < 65; i++) bytes[i] = i * 3;
    const url = btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    expect(url).toHaveLength(87);

    const zurueck = urlBase64ToUint8Array(url);

    expect(zurueck).toHaveLength(65);
    expect(zurueck[0]).toBe(4);
    expect(Array.from(zurueck)).toEqual(Array.from(bytes));
  });

  it("versteht die URL-Zeichen '-' und '_' (statt '+' und '/')", () => {
    // 0xFB 0xFF 0xBF -> "+/+/" in base64, "-_-_" in base64url.
    expect(Array.from(urlBase64ToUint8Array("-_-_"))).toEqual([0xfb, 0xff, 0xbf]);
  });

  it("ergänzt fehlendes Padding", () => {
    expect(Array.from(urlBase64ToUint8Array("YQ"))).toEqual([97]);
    expect(Array.from(urlBase64ToUint8Array("YWI"))).toEqual([97, 98]);
    expect(Array.from(urlBase64ToUint8Array("YWJj"))).toEqual([97, 98, 99]);
  });
});

describe("gehoertZuSchluessel", () => {
  const schluessel = new Uint8Array([4, 1, 2, 3]);

  it("erkennt eine Anmeldung mit demselben Schlüssel", () => {
    expect(gehoertZuSchluessel(new Uint8Array([4, 1, 2, 3]).buffer, schluessel)).toBe(true);
  });

  it("erkennt einen anderen Schlüssel gleicher Länge", () => {
    expect(gehoertZuSchluessel(new Uint8Array([4, 1, 2, 9]).buffer, schluessel)).toBe(false);
  });

  it("erkennt einen Schlüssel anderer Länge", () => {
    expect(gehoertZuSchluessel(new Uint8Array([4, 1, 2]).buffer, schluessel)).toBe(false);
  });

  it("behandelt eine Anmeldung ohne bekannten Schlüssel als nicht passend", () => {
    expect(gehoertZuSchluessel(null, schluessel)).toBe(false);
    expect(gehoertZuSchluessel(undefined, schluessel)).toBe(false);
  });
});
