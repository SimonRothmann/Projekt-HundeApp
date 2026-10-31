import { describe, expect, it } from "vitest";
import { genauigkeitsStufe, GPS_PROFIL_ABLAUF, GPS_PROFIL_LEGEN } from "./gps-profil";

describe("GPS-Profile", () => {
  it("färbt die Anzeige beim Legen: grün bis zur Filtergrenze, gelb bis 15 m", () => {
    expect(GPS_PROFIL_LEGEN.gutBis).toBe(GPS_PROFIL_LEGEN.maxAccuracyMeters);
    expect(genauigkeitsStufe(8, GPS_PROFIL_LEGEN)).toBe("gut");
    expect(genauigkeitsStufe(8.1, GPS_PROFIL_LEGEN)).toBe("mittel");
    expect(genauigkeitsStufe(15, GPS_PROFIL_LEGEN)).toBe("mittel");
    expect(genauigkeitsStufe(15.1, GPS_PROFIL_LEGEN)).toBe("schlecht");
  });

  it("färbt die Anzeige beim Ablaufen: grün bis 10 m, gelb bis zur Filtergrenze", () => {
    expect(GPS_PROFIL_ABLAUF.mittelBis).toBe(GPS_PROFIL_ABLAUF.maxAccuracyMeters);
    expect(genauigkeitsStufe(10, GPS_PROFIL_ABLAUF)).toBe("gut");
    expect(genauigkeitsStufe(25, GPS_PROFIL_ABLAUF)).toBe("mittel");
    expect(genauigkeitsStufe(25.1, GPS_PROFIL_ABLAUF)).toBe("schlecht");
  });

  it("filtert den Ablauf bewusst ohne Glättung und ohne Lockerung", () => {
    expect(GPS_PROFIL_ABLAUF.kalman).toBe(false);
    expect(GPS_PROFIL_ABLAUF.relaxedMaxAccuracyMeters).toBe(GPS_PROFIL_ABLAUF.maxAccuracyMeters);
  });

  it("lockert beim Legen von 8 auf bis zu 20 m und glättet", () => {
    expect(GPS_PROFIL_LEGEN.maxAccuracyMeters).toBe(8);
    expect(GPS_PROFIL_LEGEN.relaxedMaxAccuracyMeters).toBe(20);
    expect(GPS_PROFIL_LEGEN.kalman).toBe(true);
  });
});
