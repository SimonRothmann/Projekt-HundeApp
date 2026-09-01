#!/usr/bin/env python3
"""
Durchspielen der Kernabläufe gegen eine laufende Instanz - und danach
aufräumen, damit die Umgebung so dasteht wie vorher.

    ./scripts/e2e-test.py                      # gegen test
    ./scripts/e2e-test.py --api http://127.0.0.1:5080
    ./scripts/e2e-test.py --cleanup-only       # Reste eines Abbruchs entfernen

NUR gegen test und lokal. Das Skript schreibt: es legt Nutzer, Hunde und
Gruppen an, löscht sie wieder und hängt kurzzeitig eine Testtrainer:in an den
vorhandenen Verein. Auf einer Umgebung mit echten Mitgliedern hat das nichts
zu suchen - deshalb prüft es die Zieladresse gegen eine feste Liste und bricht
sonst ab. Bewusst OHNE Umgehungsschalter: einen Schalter, den es gibt, setzt
irgendwann jemand in der Eile.

Produktion wird von außen geprüft (Endpunkte, Katalog, Sitemap) - dort läuft
ohnehin derselbe Code, der hier auf test vollständig durchgespielt wurde.

Warum eigene Daten statt der Demo-Daten:
Test ist eine geteilte Umgebung, auf der auch von Hand geklickt wird. Ein
Skript, das die Demo-Gruppe umbaut oder mitglied1 aus dem Verein wirft,
hinterlässt Spuren, die beim nächsten manuellen Test verwirren. Deshalb legt
dieses Skript alles selbst an - Nutzer, Hunde, Gruppe - und räumt es wieder
weg. Alles trägt das Präfix "e2e-", damit Reste erkennbar bleiben.

Zwei Dinge werden bewusst NICHT angelegt:
- Vereine: es gibt keinen Lösch-Endpunkt, ein angelegter Verein bliebe für
  immer stehen. Für die Vereinsabläufe wird der vorhandene Verein benutzt und
  am Ende exakt der Ausgangszustand wiederhergestellt.
- Prüfungsordnungen: die sind globaler Katalog, den fasst ein Test nicht an.

Am Ende vergleicht das Skript einen Vorher-/Nachher-Abzug und meldet jede
Abweichung.
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import date, timedelta

PREFIX = "e2e-"
PASSWORD = "E2eTest1234!"
ADMIN = ("admin@dogity.test", "Demo1234!")

# Zieladressen, gegen die geschrieben werden darf. Alles andere wird abgelehnt.
# Eine feste Liste statt einer "test kommt im Namen vor"-Regel: die Regel würde
# eine Produktivadresse durchlassen, in der zufällig "test" steckt.
ERLAUBTE_ZIELE = {
    "api-test.dogity.net",
    "localhost",
    "127.0.0.1",
    "::1",
}

# --- Ausgabe ---------------------------------------------------------------

BOLD, GREEN, RED, YELLOW, DIM, OFF = "\033[1m", "\033[32m", "\033[31m", "\033[33m", "\033[2m", "\033[0m"
_results: list[tuple[bool, str]] = []


def check(ok: bool, text: str, detail: str = "") -> bool:
    _results.append((ok, text))
    mark = f"{GREEN}✓{OFF}" if ok else f"{RED}✗{OFF}"
    print(f"  {mark} {text}{'' if ok else f'  {RED}{detail}{OFF}'}")
    return ok


def section(title: str) -> None:
    print(f"\n{BOLD}{title}{OFF}")


# --- HTTP ------------------------------------------------------------------


class Api:
    def __init__(self, base: str):
        self.base = base.rstrip("/")

    def call(self, method: str, path: str, body=None, token: str | None = None):
        data = json.dumps(body).encode() if body is not None else None
        headers = {"Content-Type": "application/json", "User-Agent": "dogity-e2e"}
        if token:
            headers["Authorization"] = "Bearer " + token
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                raw = resp.read()
                return resp.status, (json.loads(raw) if raw else None)
        except urllib.error.HTTPError as e:
            raw = e.read().decode(errors="replace")
            try:
                return e.code, json.loads(raw)
            except json.JSONDecodeError:
                return e.code, raw[:200]
        except urllib.error.URLError as e:
            return 0, str(e)

    def login(self, email: str, password: str) -> str | None:
        status, body = self.call("POST", "/api/auth/login", {"email": email, "password": password})
        return body["token"] if status == 200 and isinstance(body, dict) else None

    def register(self, email: str, first: str, last: str) -> tuple[str, str] | None:
        """Legt einen Nutzer an oder meldet einen bestehenden an. (Token, UserId)."""
        status, body = self.call(
            "POST", "/api/auth/register",
            {"email": email, "password": PASSWORD, "firstName": first, "lastName": last},
        )
        if status not in (200, 201):
            token = self.login(email, PASSWORD)
            if token is None:
                return None
            status, body = self.call("POST", "/api/auth/login", {"email": email, "password": PASSWORD})
        return body["token"], body["userId"]


# --- Zustandsabzug ---------------------------------------------------------


def snapshot(api: Api, admin_token: str) -> dict:
    """Alles, was ein Testlauf verändern könnte - für den Vorher-/Nachher-Vergleich."""
    _, users = api.call("GET", "/api/admin/users?pageSize=200", token=admin_token)
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    state = {
        "users": sorted(u["email"] for u in users["users"]),
        "clubs": {},
    }
    for club in clubs:
        _, detail = api.call("GET", f"/api/admin/clubs/{club['id']}", token=admin_token)
        state["clubs"][club["name"]] = {
            "trainers": sorted(t["email"] for t in detail.get("trainers", [])),
            "groups": sorted(g["name"] for g in detail.get("groups", [])),
        }
    return state


def diff_state(before: dict, after: dict) -> list[str]:
    problems = []
    extra = set(after["users"]) - set(before["users"])
    missing = set(before["users"]) - set(after["users"])
    if extra:
        problems.append(f"zusätzliche Nutzer: {sorted(extra)}")
    if missing:
        problems.append(f"fehlende Nutzer: {sorted(missing)}")
    for name, b in before["clubs"].items():
        a = after["clubs"].get(name)
        if a is None:
            problems.append(f"Verein verschwunden: {name}")
            continue
        if a["trainers"] != b["trainers"]:
            problems.append(f"{name}: Trainer {b['trainers']} -> {a['trainers']}")
        if a["groups"] != b["groups"]:
            problems.append(f"{name}: Gruppen {b['groups']} -> {a['groups']}")
    for name in set(after["clubs"]) - set(before["clubs"]):
        problems.append(f"neuer Verein: {name}")
    return problems


# --- Aufräumen -------------------------------------------------------------


def cleanup(api: Api, admin_token: str, verbose: bool = True) -> None:
    """
    Entfernt alles mit e2e-Präfix. Läuft auch als Erstes vor jedem Lauf, damit
    ein abgebrochener Vorlauf den nächsten nicht blockiert.
    """
    _, users = api.call("GET", "/api/admin/users?pageSize=200", token=admin_token)
    e2e_users = [u for u in users["users"] if u["email"].startswith(PREFIX)]

    # Erst die Inhalte der Nutzer wegräumen (Gruppen, Hunde), dann die Nutzer.
    for user in e2e_users:
        token = api.login(user["email"], PASSWORD)
        if not token:
            continue
        for group in (api.call("GET", "/api/groups", token=token)[1] or []):
            if group["name"].startswith(PREFIX):
                api.call("DELETE", f"/api/groups/{group['id']}", token=token)
        for dog in (api.call("GET", "/api/dogs", token=token)[1] or []):
            api.call("DELETE", f"/api/dogs/{dog['id']}", token=token)
        # Aus allen Vereinen austreten, damit keine Mitgliedschaft zurückbleibt.
        for club in (api.call("GET", "/api/clubs", token=token)[1] or []):
            api.call("DELETE", f"/api/clubs/{club['id']}/membership", token=token)

    for user in e2e_users:
        status, _ = api.call("DELETE", f"/api/admin/users/{user['id']}", token=admin_token)
        if verbose:
            print(f"  {DIM}Nutzer entfernt: {user['email']} ({status}){OFF}")

    # Verwaiste e2e-Gruppen (falls ihr Trainer schon weg ist).
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    for club in clubs or []:
        _, detail = api.call("GET", f"/api/admin/clubs/{club['id']}", token=admin_token)
        for group in detail.get("groups", []):
            if group["name"].startswith(PREFIX) and verbose:
                print(f"  {YELLOW}Achtung: Gruppe '{group['name']}' konnte nicht entfernt werden{OFF}")


# --- Die Abläufe -----------------------------------------------------------


def run_scenarios(api: Api, admin_token: str) -> None:
    today = date.today()

    section("Aufbau: eigene Nutzer, Hund und Gruppe")
    owner_token, owner_id = api.register(f"{PREFIX}besitzer@dogity.test", "Erika", "E2E")
    trainer_token, trainer_id = api.register(f"{PREFIX}trainer@dogity.test", "Tom", "E2E")
    helper_token, helper_id = api.register(f"{PREFIX}helfer@dogity.test", "Hanna", "E2E")
    check(all([owner_token, trainer_token, helper_token]), "drei Wegwerf-Nutzer angelegt")

    status, dog = api.call("POST", "/api/dogs", {"name": f"{PREFIX}Rex", "breed": "Testhund", "gender": 0}, owner_token)
    check(status in (200, 201), "Hund angelegt", str(dog))
    dog_id = dog["id"]

    status, group = api.call("POST", "/api/groups", {"name": f"{PREFIX}Gruppe", "description": None}, trainer_token)
    check(status in (200, 201), "Gruppe angelegt", str(group))
    group_id = group["id"]
    api.call("POST", f"/api/groups/{group_id}/members", {"email": f"{PREFIX}besitzer@dogity.test"}, trainer_token)

    # ---------------------------------------------------------------- Punkt 1
    section("Punkt 1 – Betreuung beenden")
    api.call("POST", f"/api/groups/{group_id}/trainer-assignments", {"memberId": owner_id, "dogId": dog_id}, trainer_token)
    check(api.call("GET", f"/api/trainings?dogId={dog_id}", token=trainer_token)[0] == 200,
          "Trainer sieht das Tagebuch des betreuten Hundes")
    status, _ = api.call("DELETE", f"/api/groups/{group_id}/trainer-assignments/{trainer_id}/{dog_id}", token=trainer_token)
    check(status == 204, "Betreuung beendet", str(status))
    check(api.call("GET", f"/api/trainings?dogId={dog_id}", token=trainer_token)[0] == 404,
          "Zugriff ist damit weg")
    status, _ = api.call("POST", f"/api/groups/{group_id}/trainer-assignments", {"memberId": owner_id, "dogId": dog_id}, trainer_token)
    check(status == 204, "erneute Betreuung möglich (Soft-Delete wiederbelebt)", str(status))

    # ---------------------------------------------------------------- Punkt 3
    section("Punkt 3 – Wiederaufnahme nach dem Entfernen")
    api.call("DELETE", f"/api/groups/{group_id}/members/{owner_id}", token=trainer_token)
    status, body = api.call("POST", f"/api/groups/{group_id}/members", {"email": f"{PREFIX}besitzer@dogity.test"}, trainer_token)
    check(status == 204, "entferntes Mitglied wieder aufnehmbar", str(body))

    api.call("POST", f"/api/groups/{group_id}/join-requests", token=helper_token)
    api.call("POST", f"/api/groups/{group_id}/join-requests/{helper_id}/reject", token=trainer_token)
    status, body = api.call("POST", f"/api/groups/{group_id}/join-requests", token=helper_token)
    check(status == 204, "nach Ablehnung erneut bewerbbar", str(body))
    api.call("POST", f"/api/groups/{group_id}/join-requests/{helper_id}/reject", token=trainer_token)

    api.call("POST", f"/api/dogs/{dog_id}/owners", {"email": f"{PREFIX}helfer@dogity.test"}, owner_token)
    api.call("DELETE", f"/api/dogs/{dog_id}/owners/{helper_id}", token=owner_token)
    status, body = api.call("POST", f"/api/dogs/{dog_id}/owners", {"email": f"{PREFIX}helfer@dogity.test"}, owner_token)
    check(status == 204, "entfernter Mitbesitzer wieder hinzufügbar", str(body))
    api.call("DELETE", f"/api/dogs/{dog_id}/owners/{helper_id}", token=owner_token)

    # ---------------------------------------------------------------- Punkt 4
    section("Punkt 4 – Wiedervorlage folgt Löschen und Verschieben")
    _, sports = api.call("GET", "/api/sports", token=owner_token)
    bh = next(s for s in sports if s["name"] == "Begleithundeprüfung")
    _, exercises = api.call("GET", f"/api/sports/{bh['id']}/exercises", token=owner_token)
    exercise = next(e for e in exercises if e["name"] == "Fußarbeit")

    _, goal = api.call("POST", "/api/goals", {
        "dogId": dog_id, "sportId": bh["id"], "regulationId": None,
        "targetDate": str(today + timedelta(days=90)), "notes": None, "isCustom": False,
    }, owner_token)

    def mastery_of(name: str) -> int | None:
        st, rows = api.call("GET", f"/api/goals/{goal['id']}/weightable-exercises", token=owner_token)
        if st != 200:
            return None
        hit = [r for r in rows if r["exerciseName"] == name]
        return hit[0]["masteryStatus"] if hit else None

    before = mastery_of("Fußarbeit")
    status, session = api.call("POST", "/api/trainings", {
        "dogId": dog_id, "date": str(today), "durationMinutes": 30, "notes": None,
        "startTime": "17:30:00", "latitude": 53.55, "longitude": 9.99, "locationName": f"{PREFIX}Platz",
        "exercises": [{"exerciseId": exercise["id"], "rating": 5, "difficulty": 0, "success": True, "notes": "sauber"}],
    }, owner_token)
    check(status in (200, 201), "Training mit Ort, Uhrzeit und Übungskommentar angelegt", str(session))
    check(session["startTime"] is not None and session["locationName"] == f"{PREFIX}Platz",
          "Ort und Uhrzeit sind gespeichert")
    check(session["exercises"][0]["notes"] == "sauber", "Kommentar zur Übung ist gespeichert")
    after_log = mastery_of("Fußarbeit")
    check(after_log != before, f"Wiedervorlage hat sich bewegt ({before} -> {after_log})")

    api.call("DELETE", f"/api/trainings/{session['id']}", token=owner_token)
    check(mastery_of("Fußarbeit") == before,
          f"nach dem Löschen wieder auf dem Ausgangswert ({before})", str(mastery_of("Fußarbeit")))

    # ---------------------------------------------------------------- Punkt 7
    section("Punkt 7 – 400 für Eingabefehler, 404 für Unbekanntes")
    _, session2 = api.call("POST", "/api/trainings", {
        "dogId": dog_id, "date": str(today), "durationMinutes": 20, "notes": None,
        "exercises": [{"exerciseId": exercise["id"], "rating": 3, "difficulty": 0, "success": True, "notes": None}],
    }, owner_token)
    tex = session2["exercises"][0]["id"]
    status, body = api.call("PUT", f"/api/trainings/exercises/{tex}", {"rating": 9, "success": True, "notes": None}, owner_token)
    check(status == 400, "unsinnige Bewertung -> 400", f"war {status}")
    check("zwischen 1 und 5" in json.dumps(body, ensure_ascii=False), "mit lesbarer Meldung", str(body))
    zero = "00000000-0000-0000-0000-000000000000"
    check(api.call("PUT", f"/api/trainings/exercises/{zero}", {"rating": 3, "success": True, "notes": None}, owner_token)[0] == 404,
          "unbekannte Übung -> 404")

    status, updated = api.call("PUT", f"/api/trainings/exercises/{tex}", {"rating": 5, "success": True, "notes": "korrigiert"}, owner_token)
    check(status == 200 and updated["exercises"][0]["rating"] == 5, "Bewertung nachträglich änderbar", str(status))
    api.call("DELETE", f"/api/trainings/{session2['id']}", token=owner_token)

    # ---------------------------------------------------------------- Punkt 5
    section("Punkt 5 – Gruppe auflösen")
    status, co = api.call("POST", f"/api/groups/{group_id}/co-trainers", {"email": f"{PREFIX}helfer@dogity.test"}, trainer_token)
    check(status == 204, "weitere Trainer:in hinzugefügt", str(co))
    _, detail = api.call("GET", f"/api/groups/{group_id}", token=trainer_token)
    check(len(detail["trainers"]) == 2, "Gruppe hat zwei Trainer:innen", str(detail["trainers"]))
    check(api.call("GET", "/api/groups", token=helper_token)[0] == 200 and
          any(g["id"] == group_id for g in api.call("GET", "/api/groups", token=helper_token)[1]),
          "sie sieht die Gruppe in ihrer eigenen Übersicht")

    status, body = api.call("DELETE", f"/api/groups/{group_id}", token=helper_token)
    check(status == 204, "Gruppe aufgelöst", str(body))
    check(api.call("GET", f"/api/groups/{group_id}", token=trainer_token)[0] == 404, "danach nicht mehr abrufbar")

    # ---------------------------------------------------------------- Punkt 2
    section("Punkt 2 – Vereinsaustritt räumt auf")
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    if not clubs:
        check(False, "kein Verein vorhanden - Ablauf übersprungen")
        return
    club_id = clubs[0]["id"]

    api.call("POST", f"/api/admin/clubs/{club_id}/members", {"email": f"{PREFIX}besitzer@dogity.test"}, admin_token)
    api.call("POST", f"/api/admin/clubs/{club_id}/trainers", {"email": f"{PREFIX}trainer@dogity.test"}, admin_token)
    status, club_group = api.call("POST", "/api/groups", {"name": f"{PREFIX}Vereinsgruppe", "description": None, "clubId": club_id}, trainer_token)
    check(status in (200, 201), "Vereinsgruppe angelegt", str(club_group))
    cg_id = club_group["id"]
    api.call("POST", f"/api/groups/{cg_id}/members", {"email": f"{PREFIX}besitzer@dogity.test"}, trainer_token)
    api.call("POST", f"/api/groups/{cg_id}/trainer-assignments", {"memberId": owner_id, "dogId": dog_id}, trainer_token)

    check(api.call("GET", f"/api/trainings?dogId={dog_id}", token=trainer_token)[0] == 200,
          "Vereinstrainer sieht den Hund")
    groups_before = api.call("GET", f"/api/clubs/{club_id}/groups", token=owner_token)
    check(groups_before[0] == 200 and any(g["id"] == cg_id and g["myRelation"] == 2 for g in groups_before[1]),
          "Besitzer ist Mitglied der Vereinsgruppe")

    status, _ = api.call("DELETE", f"/api/clubs/{club_id}/membership", token=owner_token)
    check(status == 204, "Verein verlassen", str(status))
    check(api.call("GET", f"/api/trainings?dogId={dog_id}", token=trainer_token)[0] == 404,
          "Trainerzugriff auf den Hund ist weg")
    check(api.call("GET", f"/api/trainings?dogId={dog_id}", token=owner_token)[0] == 200,
          "eigene Trainingsdaten sind unberührt")

    # ---------------------------------------------------------------- Punkt 6
    section("Punkt 6 – Vereinstrainer:in ohne Mitgliedschaft sieht die Gruppen")
    status, groups = api.call("GET", f"/api/clubs/{club_id}/groups", token=trainer_token)
    check(status == 200 and any(g["id"] == cg_id for g in groups),
          "Trainer:in sieht die Gruppen ihres Vereins", f"{status} {groups}")
    check(all(g["myRelation"] == 3 for g in groups if g["id"] == cg_id),
          "und zwar als Trainer:in, nicht mit Beitreten-Knopf")

    api.call("DELETE", f"/api/groups/{cg_id}", token=trainer_token)
    api.call("DELETE", f"/api/admin/clubs/{club_id}/trainers/{trainer_id}", token=admin_token)


# --- Breitentest über die übrigen Funktionen -------------------------------


def run_feature_sweep(api: Api, admin_token: str) -> None:
    """
    Zweiter Teil: die Funktionen, die nicht aus den Fehlerkorrekturen stammen.
    Ziel ist Breite, nicht Tiefe - jede Funktion einmal auf dem Hauptweg, damit
    ein Deploy nicht unbemerkt etwas abräumt.
    """
    today = date.today()
    owner_token, owner_id = api.register(f"{PREFIX}sweep@dogity.test", "Sabine", "Sweep")
    trainer_token, trainer_id = api.register(f"{PREFIX}sweeptrainer@dogity.test", "Timo", "Sweep")

    section("Öffentlich (ohne Anmeldung)")
    status, sports = api.call("GET", "/api/sports")
    check(status == 200 and len(sports) >= 15, f"Sportartenkatalog anonym lesbar ({len(sports) if status==200 else status})")
    bh = next((x for x in sports if x["name"] == "Begleithundeprüfung"), None)
    status, regs = api.call("GET", f"/api/sports/{bh['id']}/regulations")
    check(status == 200 and regs, "Prüfungsordnungen anonym lesbar")
    status, detail = api.call("GET", f"/api/sports/regulations/{regs[0]['id']}")
    check(status == 200 and detail["exercises"], "Übungen einer Prüfungsordnung anonym lesbar")
    check(api.call("GET", "/api/dogs")[0] == 401, "Hundeliste ist ohne Anmeldung gesperrt")

    section("Hund: Stammdaten, Bild, Archiv")
    _, dog = api.call("POST", "/api/dogs", {"name": f"{PREFIX}Sweepy", "breed": "Mischling", "gender": 1}, owner_token)
    dog_id = dog["id"]
    status, _ = api.call("PUT", f"/api/dogs/{dog_id}", {
        "name": f"{PREFIX}Sweepy", "breed": "Border Collie", "birthday": "2022-03-15",
        "gender": 1, "imageUrl": None, "notes": "Testnotiz",
    }, owner_token)
    _, reread = api.call("GET", f"/api/dogs/{dog_id}", token=owner_token)
    check(status == 200 and reread["birthday"] == "2022-03-15" and reread["breed"] == "Border Collie",
          "Stammdaten änderbar (Rasse, Geburtsdatum)", str(reread))

    # 1x1-PNG als Data-URI
    png = ("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ"
           "AAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")
    check(api.call("PUT", f"/api/dogs/{dog_id}/image", {"dataUrl": png}, owner_token)[0] == 204, "Hundebild hochladen")
    status, img = api.call("GET", f"/api/dogs/{dog_id}/image", token=owner_token)
    check(status == 200 and img["dataUrl"].startswith("data:image/png"), "Hundebild wieder abrufbar")
    check(api.call("PUT", f"/api/dogs/{dog_id}/image", {"dataUrl": "data:text/html;base64,PGI+"}, owner_token)[0] == 400,
          "fremder MIME-Typ wird abgewiesen")
    check(api.call("DELETE", f"/api/dogs/{dog_id}/image", token=owner_token)[0] == 204, "Hundebild löschbar")

    check(api.call("PUT", f"/api/dogs/{dog_id}/archive", {"archived": True}, owner_token)[0] == 204, "Hund archivierbar")
    check(api.call("PUT", f"/api/dogs/{dog_id}/archive", {"archived": False}, owner_token)[0] == 204, "und wieder aktivierbar")

    section("Ziel und Trainingsplan")
    status, goal = api.call("POST", "/api/goals", {
        "dogId": dog_id, "sportId": bh["id"], "regulationId": regs[0]["id"],
        "targetDate": str(today + timedelta(days=120)), "notes": "Sweep", "isCustom": False,
    }, owner_token)
    check(status in (200, 201), "Ziel mit Prüfungsordnung angelegt", str(goal))
    goal_id = goal["id"]
    check(goal["trainingPlan"] is not None and len(goal["trainingPlan"]["items"]) > 0,
          f"Trainingsplan automatisch erzeugt ({len(goal['trainingPlan']['items']) if goal['trainingPlan'] else 0} Wochenziele)")

    status, updated = api.call("PUT", f"/api/goals/{goal_id}/config", {"weeklyExerciseCount": 4, "trainingDaysPerWeek": 3}, owner_token)
    check(status == 200 and updated["weeklyExerciseCount"] == 4, "Plan-Konfiguration änderbar", str(status))
    status, updated = api.call("PUT", f"/api/goals/{goal_id}/weeks/2/config", {"trainingDaysPerWeek": 1}, owner_token)
    check(status == 200 and any(w["weekNumber"] == 2 for w in updated["weekConfigs"]), "einzelne Woche abweichend konfigurierbar")

    status, updated = api.call("POST", f"/api/goals/{goal_id}/plan-items",
                               {"weekNumber": 3, "exerciseId": None, "freeTextLabel": "Kopfarbeit", "repetitionsTarget": 2, "dayIndex": 1}, owner_token)
    check(status == 200, "eigenes Wochenziel hinzufügbar", str(status))
    item = next((i for i in updated["trainingPlan"]["items"] if i["freeTextLabel"] == "Kopfarbeit"), None)
    check(item is not None, "und im Plan sichtbar")
    if item:
        check(api.call("PUT", f"/api/goals/{goal_id}/plan-items/{item['id']}",
                       {"weekNumber": 3, "exerciseId": None, "freeTextLabel": "Kopfarbeit intensiv", "repetitionsTarget": 3, "dayIndex": 1},
                       owner_token)[0] == 200, "Wochenziel änderbar")
        check(api.call("DELETE", f"/api/goals/{goal_id}/plan-items/{item['id']}", token=owner_token)[0] == 200, "Wochenziel entfernbar")

    check(api.call("PUT", f"/api/goals/{goal_id}/regenerate-week", {"weekNumber": 4}, owner_token)[0] == 200, "Woche neu generierbar")
    status, weightable = api.call("GET", f"/api/goals/{goal_id}/weightable-exercises", token=owner_token)
    check(status == 200 and weightable, f"Übungsgewichtung abrufbar ({len(weightable) if status==200 else 0} Übungen)")
    if status == 200 and weightable:
        ex_id = weightable[0]["exerciseId"]
        check(api.call("PUT", f"/api/goals/{goal_id}/exercises/{ex_id}/priority", {"value": 2}, owner_token)[0] == 204,
              "Gewichtung setzbar")
        check(api.call("PUT", f"/api/goals/{goal_id}/exercises/{ex_id}/priority", {"value": 9}, owner_token)[0] == 400,
              "Gewichtung außerhalb −2..+2 abgewiesen")

    section("Training: Zusammenfassen, Verschieben, Notizen")
    _, ex_list = api.call("GET", f"/api/sports/{bh['id']}/exercises", token=owner_token)
    ex1, ex2 = ex_list[0], ex_list[1]
    _, s1 = api.call("POST", "/api/trainings", {
        "dogId": dog_id, "date": str(today - timedelta(days=2)), "durationMinutes": 30, "notes": "erste Einheit",
        "exercises": [{"exerciseId": ex1["id"], "rating": 4, "difficulty": 0, "success": True, "notes": None}],
    }, owner_token)
    _, s2 = api.call("POST", "/api/trainings", {
        "dogId": dog_id, "date": str(today - timedelta(days=2)), "durationMinutes": 20, "notes": "zweite Einheit",
        "exercises": [{"exerciseId": ex2["id"], "rating": 3, "difficulty": 0, "success": True, "notes": None}],
    }, owner_token)
    check(s1["id"] == s2["id"] and len(s2["exercises"]) == 2 and s2["durationMinutes"] == 50,
          "zweite Einheit am selben Tag wird zusammengefasst (50 Min., 2 Übungen)", str(s2["durationMinutes"]))

    status, moved = api.call("PUT", f"/api/trainings/{s2['id']}/date", {"date": str(today - timedelta(days=5))}, owner_token)
    check(status == 200 and moved["date"] == str(today - timedelta(days=5)), "Trainingstag verschiebbar", str(status))
    check(api.call("PUT", f"/api/trainings/{s2['id']}/notes", {"notes": "Tagesnotiz"}, owner_token)[0] == 204, "Tagesnotiz änderbar")
    tex = moved["exercises"][0]["id"]
    check(api.call("PUT", f"/api/trainings/exercises/{tex}/notes", {"notes": "Übungsnotiz"}, owner_token)[0] == 204, "Übungsnotiz änderbar")
    status, ctx = api.call("PUT", f"/api/trainings/{s2['id']}/context",
                           {"startTime": "09:15:00", "latitude": 48.14, "longitude": 11.58, "locationName": f"{PREFIX}Wiese"}, owner_token)
    check(status == 200 and ctx["locationName"] == f"{PREFIX}Wiese", "Ort und Uhrzeit nachträglich setzbar")
    status, locations = api.call("GET", "/api/trainings/locations", token=owner_token)
    check(status == 200 and any(l["name"] == f"{PREFIX}Wiese" for l in locations), "Ort landet in den zuletzt genutzten")

    section("Trainer: Feedback und Übungsbewertung")
    _, grp = api.call("POST", "/api/groups", {"name": f"{PREFIX}Sweepgruppe", "description": None}, trainer_token)
    api.call("POST", f"/api/groups/{grp['id']}/members", {"email": f"{PREFIX}sweep@dogity.test"}, trainer_token)
    api.call("POST", f"/api/groups/{grp['id']}/trainer-assignments", {"memberId": owner_id, "dogId": dog_id}, trainer_token)
    check(api.call("PUT", f"/api/trainings/{s2['id']}/feedback", {"feedback": "Gut gemacht"}, trainer_token)[0] == 204,
          "Trainer kann Gesamt-Feedback geben")
    check(api.call("PUT", f"/api/trainings/{s2['id']}/feedback", {"feedback": "Selbstlob"}, owner_token)[0] == 400,
          "Besitzer kann sich selbst kein Trainer-Feedback geben")
    check(api.call("PUT", f"/api/trainings/exercises/{tex}/trainer-rating", {"rating": 4, "note": "sauber"}, trainer_token)[0] == 204,
          "Trainer kann eine Übung bewerten")
    status, to_rate = api.call("GET", "/api/trainings/trainer/sessions", token=trainer_token)
    check(status == 200, f"Bewertungsliste abrufbar ({len(to_rate) if status==200 else status} offen)")
    status, supervised = api.call("GET", "/api/dogs/supervised", token=trainer_token)
    check(status == 200 and any(d["id"] == dog_id for d in supervised), "betreuter Hund erscheint in der Trainerliste")

    section("Statistik und Benachrichtigungen")
    check(api.call("GET", "/api/stats/dashboard", token=owner_token)[0] == 200, "Dashboard-Statistik")
    check(api.call("GET", f"/api/stats/dogs/{dog_id}/exercises", token=owner_token)[0] == 200, "Übungsstatistik je Hund")
    check(api.call("GET", f"/api/stats/dogs/{dog_id}/tracks", token=owner_token)[0] == 200, "Fährtenstatistik je Hund")
    status, unread = api.call("GET", "/api/notifications/unread-count", token=owner_token)
    check(status == 200, f"ungelesene Benachrichtigungen ({unread})")
    check(api.call("POST", "/api/notifications/read-all", token=owner_token)[0] == 204, "alle als gelesen markierbar")

    section("Gruppentraining und Terminplanung")
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    club_id = clubs[0]["id"] if clubs else None
    if club_id:
        api.call("POST", f"/api/admin/clubs/{club_id}/trainers", {"email": f"{PREFIX}sweeptrainer@dogity.test"}, admin_token)
        status, lib = api.call("GET", f"/api/group-training/clubs/{club_id}/library", token=trainer_token)
        check(status == 200, "Trainingsbibliothek abrufbar", str(status))
        status, gex = api.call("POST", f"/api/group-training/clubs/{club_id}/exercises",
                               {"category": 0, "title": f"{PREFIX}Baustein", "focus": "Test", "durationMinutes": 10,
                                "description": "Testbaustein", "examTargets": 0}, trainer_token)
        check(status in (200, 201), "Baustein anlegbar", str(gex))
        if status in (200, 201):
            status, unit = api.call("POST", f"/api/group-training/clubs/{club_id}/units",
                                    {"category": 0, "title": f"{PREFIX}Einheit", "description": None, "exerciseIds": [gex["id"]]}, trainer_token)
            check(status in (200, 201), "Einheit aus Bausteinen zusammenstellbar", str(unit))
            if status in (200, 201):
                check(api.call("POST", f"/api/group-training/units/{unit['id']}/duplicate", token=trainer_token)[0] in (200, 201),
                      "Einheit duplizierbar")

        _, club_group = api.call("POST", "/api/groups", {"name": f"{PREFIX}Termingruppe", "description": None, "clubId": club_id}, trainer_token)
        starts = (date.today() + timedelta(days=7)).isoformat() + "T18:00:00+00:00"
        status, sess = api.call("POST", f"/api/group-training/schedule/clubs/{club_id}/sessions", {
            "groupId": club_group["id"], "category": 0, "startsAt": starts, "durationMinutes": 60,
            "location": "Platz", "notes": None, "trainerUserIds": [trainer_id], "items": [],
        }, trainer_token)
        check(status in (200, 201), "Trainingstermin anlegbar", str(sess))
        check(api.call("GET", f"/api/group-training/schedule/clubs/{club_id}", token=trainer_token)[0] == 200, "Terminliste abrufbar")
        check(api.call("GET", "/api/group-training/schedule/mine", token=trainer_token)[0] == 200, "eigene Termine abrufbar")
        check(api.call("GET", f"/api/group-training/schedule/clubs/{club_id}/generate-content", token=trainer_token)[0] == 200,
              "Mix-Generator liefert Inhalte")
        if status in (200, 201):
            check(api.call("POST", f"/api/group-training/schedule/sessions/{sess['id']}/cancel", token=trainer_token)[0] == 204, "Termin absagbar")
            check(api.call("DELETE", f"/api/group-training/schedule/sessions/{sess['id']}", token=trainer_token)[0] == 204, "Termin löschbar")

        # Gruppe mit Terminen darf nicht einfach verschwinden
        api.call("DELETE", f"/api/groups/{club_group['id']}", token=trainer_token)
        api.call("DELETE", f"/api/admin/clubs/{club_id}/trainers/{trainer_id}", token=admin_token)

    section("Verein: Anfrage, Freigabe, Beförderung")
    if club_id:
        check(api.call("POST", f"/api/clubs/{club_id}/join-requests", token=owner_token)[0] in (200, 201, 204),
              "Beitrittsanfrage stellbar")
        check(api.call("POST", f"/api/clubs/{club_id}/join-requests", token=owner_token)[0] == 400,
              "zweite Anfrage wird abgewiesen")
        status, pending = api.call("GET", f"/api/clubs/{club_id}/join-requests", token=admin_token)
        check(status in (200, 404), "Anfrageliste abrufbar (nur für Vereinstrainer)", str(status))

    section("Profil")
    check(api.call("PUT", "/api/profile/password",
                   {"currentPassword": PASSWORD, "newPassword": PASSWORD}, owner_token)[0] in (204, 400),
          "Passwortänderung erreichbar")
    check(api.call("PUT", "/api/profile/password",
                   {"currentPassword": "falschesPasswort", "newPassword": "NeuesPasswort1!"}, owner_token)[0] == 400,
          "falsches aktuelles Passwort wird abgewiesen")

    section("Admin")
    status, users = api.call("GET", "/api/admin/users", token=admin_token)
    check(status == 200 and users["totalCount"] >= 1, "Nutzerliste abrufbar")
    check(api.call("GET", "/api/admin/stats", token=admin_token)[0] == 200, "Kennzahlen abrufbar")
    check(api.call("GET", "/api/admin/users", token=owner_token)[0] == 403, "Nicht-Admin wird abgewiesen")
    check(api.call("POST", f"/api/admin/users/{owner_id}/lock", token=admin_token)[0] == 204, "Nutzer sperrbar")
    check(api.call("POST", "/api/auth/login", {"email": f"{PREFIX}sweep@dogity.test", "password": PASSWORD})[0] != 200,
          "gesperrter Nutzer kommt nicht mehr rein")
    check(api.call("POST", f"/api/admin/users/{owner_id}/unlock", token=admin_token)[0] == 204, "und wieder entsperrbar")
    check(api.call("POST", "/api/auth/login", {"email": f"{PREFIX}sweep@dogity.test", "password": PASSWORD})[0] == 200,
          "danach wieder anmeldbar")


# --- Einstieg --------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default="https://api-test.dogity.net")
    parser.add_argument("--admin-email", default=ADMIN[0])
    parser.add_argument("--admin-password", default=ADMIN[1])
    parser.add_argument("--cleanup-only", action="store_true", help="nur Reste eines Abbruchs entfernen")
    args = parser.parse_args()

    host = urllib.parse.urlparse(args.api).hostname or ""
    if host not in ERLAUBTE_ZIELE:
        print(f"{RED}{args.api} ist kein zugelassenes Ziel.{OFF}")
        print("  Dieses Skript legt Nutzer, Hunde und Gruppen an und löscht sie wieder -")
        print("  das gehört nicht auf eine Umgebung mit echten Mitgliedern.")
        print(f"  Zugelassen: {', '.join(sorted(ERLAUBTE_ZIELE))}")
        return 2

    api = Api(args.api)
    admin_password = os.environ.get("DOGITY_ADMIN_PASSWORD", args.admin_password)
    admin_token = api.login(args.admin_email, admin_password)
    if not admin_token:
        print(f"{RED}Admin-Anmeldung an {args.api} als {args.admin_email} fehlgeschlagen.{OFF}")
        if args.admin_email == ADMIN[0]:
            print(f"  {DIM}Das ist der Demo-Admin aus dem DemoDataSeeder. Fehlt er, lief der Seeder")
            print(f"  auf dieser Instanz nicht - dann steht ASPNETCORE_ENVIRONMENT nicht auf")
            print(f"  Development, oder die Instanz ist noch nicht hochgefahren.{OFF}")
        print("  Mit einem anderen Admin-Zugang:")
        print(f"    {BOLD}DOGITY_ADMIN_PASSWORD=... ./scripts/e2e-test.py --api {args.api} --admin-email ...{OFF}")
        print(f"  {DIM}Der Zugang braucht die Rolle ADMIN (Nutzer anlegen/löschen, Vereine verwalten).{OFF}")
        return 2

    print(f"{BOLD}Ziel:{OFF} {args.api}")

    if args.cleanup_only:
        section("Reste entfernen")
        cleanup(api, admin_token)
        return 0

    section("Zustand vorher sichern")
    before = snapshot(api, admin_token)
    print(f"  {DIM}{len(before['users'])} Nutzer, {len(before['clubs'])} Verein(e){OFF}")
    cleanup(api, admin_token, verbose=False)  # Reste eines Abbruchs

    try:
        run_scenarios(api, admin_token)
        run_feature_sweep(api, admin_token)
    finally:
        section("Aufräumen")
        cleanup(api, admin_token)
        after = snapshot(api, admin_token)
        problems = diff_state(before, after)
        check(not problems, "Ausgangszustand wiederhergestellt", "; ".join(problems))

    failed = [t for ok, t in _results if not ok]
    section("Ergebnis")
    if failed:
        print(f"  {RED}{len(failed)} von {len(_results)} Prüfungen fehlgeschlagen:{OFF}")
        for t in failed:
            print(f"    - {t}")
        return 1
    print(f"  {GREEN}Alle {len(_results)} Prüfungen bestanden.{OFF}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
