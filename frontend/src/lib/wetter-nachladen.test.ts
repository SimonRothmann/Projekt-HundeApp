import { beforeEach, describe, expect, it } from "vitest";
import type { GpsPoint, GpsTrack, GpsWalkRun, TrainingSession } from "@/lib/types";
import {
  faehrteWetterOffen,
  schreibzugriffMelden,
  trainingWetterOffen,
  WETTER_FRISCH_MS,
  WETTER_NACHLADEN_NACH_MS,
  wetterMerkerZuruecksetzen,
  wetterNachladenIn,
  zuletztGespeichertUm,
} from "./wetter-nachladen";

const punkt = (pointType: number): GpsPoint => ({ pointType }) as GpsPoint;

describe("wetterNachladenIn", () => {
  it("lädt nach, wenn gerade erst gespeichert wurde", () => {
    expect(wetterNachladenIn(1_000_000, 1_000_000)).toBe(WETTER_NACHLADEN_NACH_MS);
  });

  it("rechnet die schon vergangene Zeit ab", () => {
    expect(wetterNachladenIn(1_000_000, 1_002_000)).toBe(WETTER_NACHLADEN_NACH_MS - 2000);
  });

  it("wartet mindestens eine Sekunde", () => {
    expect(wetterNachladenIn(1_000_000, 1_000_000 + WETTER_NACHLADEN_NACH_MS + 500)).toBe(1000);
  });

  it("lädt nicht nach, wenn das Speichern zu lange her ist oder nie stattfand", () => {
    expect(wetterNachladenIn(1_000_000, 1_000_000 + WETTER_FRISCH_MS + 1)).toBeNull();
    expect(wetterNachladenIn(0, 5000)).toBeNull();
    // Uhr zurückgestellt: lieber nichts tun.
    expect(wetterNachladenIn(2_000_000, 1_000_000)).toBeNull();
  });
});

describe("schreibzugriffMelden", () => {
  beforeEach(() => wetterMerkerZuruecksetzen());

  it("merkt sich das Speichern einer Fährte, eines Ablaufs und eines Trainings", () => {
    schreibzugriffMelden("POST", "/api/gps-tracks", 10);
    expect(zuletztGespeichertUm()).toBe(10);
    schreibzugriffMelden("POST", "/api/gps-tracks/abc/walk-runs", 20);
    expect(zuletztGespeichertUm()).toBe(20);
    schreibzugriffMelden("POST", "/api/trainings", 30);
    expect(zuletztGespeichertUm()).toBe(30);
    schreibzugriffMelden("PUT", "/api/trainings/abc/context", 40);
    expect(zuletztGespeichertUm()).toBe(40);
  });

  it("ignoriert Lesen, Löschen und fremde Pfade", () => {
    schreibzugriffMelden(undefined, "/api/trainings", 10);
    schreibzugriffMelden("GET", "/api/gps-tracks", 10);
    schreibzugriffMelden("DELETE", "/api/trainings/abc", 10);
    schreibzugriffMelden("POST", "/api/dogs", 10);
    schreibzugriffMelden("POST", "/api/trainings-plan", 10);
    expect(zuletztGespeichertUm()).toBe(0);
  });
});

describe("trainingWetterOffen", () => {
  const mitOrt: Pick<TrainingSession, "startTime" | "latitude" | "longitude" | "temperatureC"> = {
    startTime: "09:30:00",
    latitude: 48.9,
    longitude: 8.4,
    temperatureC: null,
  };

  it("steht aus, solange Ort und Uhrzeit da sind, aber keine Temperatur", () => {
    expect(trainingWetterOffen(mitOrt)).toBe(true);
  });

  it("steht nicht aus, wenn die Temperatur da ist - auch bei 0 Grad", () => {
    expect(trainingWetterOffen({ ...mitOrt, temperatureC: 0 })).toBe(false);
  });

  it("steht nicht aus, wenn Ort oder Uhrzeit fehlen (dann gibt es nie ein Wetter)", () => {
    expect(trainingWetterOffen({ ...mitOrt, startTime: null })).toBe(false);
    expect(trainingWetterOffen({ ...mitOrt, latitude: null })).toBe(false);
    expect(trainingWetterOffen({ ...mitOrt, longitude: null })).toBe(false);
  });
});

describe("faehrteWetterOffen", () => {
  const lauf = (anzahlPunkte: number): GpsWalkRun => ({ points: Array.from({ length: anzahlPunkte }, () => ({})) }) as unknown as GpsWalkRun;
  const spur = (
    punkte: GpsPoint[],
    laid: number | null,
    search: number | null = null,
    laeufe: GpsWalkRun[] = [],
  ): Pick<GpsTrack, "points" | "walkRuns" | "laidTemperatureC" | "searchTemperatureC"> => ({
    points: punkte,
    walkRuns: laeufe,
    laidTemperatureC: laid,
    searchTemperatureC: search,
  });

  it("steht aus bei gelegter Spur ohne Temperatur", () => {
    expect(faehrteWetterOffen(spur([punkt(0), punkt(0)], null))).toBe(true);
  });

  it("steht nicht aus, wenn die Temperatur da ist und kein Ablauf gespeichert wurde", () => {
    expect(faehrteWetterOffen(spur([punkt(0)], 0))).toBe(false);
  });

  it("steht aus, wenn nach dem Ablauf die Temperatur beim Suchen noch fehlt", () => {
    expect(faehrteWetterOffen(spur([punkt(0)], 12, null, [lauf(3)]))).toBe(true);
  });

  it("steht nicht aus, wenn auch die Temperatur beim Suchen da ist - auch bei 0 Grad", () => {
    expect(faehrteWetterOffen(spur([punkt(0)], 12, 0, [lauf(3)]))).toBe(false);
  });

  it("zählt einen Ablauf ohne Punkte nicht - dafür fragt der Server nichts an", () => {
    expect(faehrteWetterOffen(spur([punkt(0)], 12, null, [lauf(0)]))).toBe(false);
  });

  it("zählt nur automatische Punkte - ein Marker allein liefert keinen Ort fürs Wetter", () => {
    expect(faehrteWetterOffen(spur([punkt(1)], null))).toBe(false);
    expect(faehrteWetterOffen(spur([], null))).toBe(false);
    expect(faehrteWetterOffen(spur([punkt(1)], null, null, [lauf(3)]))).toBe(false);
  });
});
