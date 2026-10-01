#!/usr/bin/env python3
"""
Durchspielen der Kernabläufe gegen eine laufende Instanz - und danach
aufräumen, damit die Umgebung so dasteht wie vorher.

    ./scripts/e2e-test.py                      # gegen test
    ./scripts/e2e-test.py --api http://127.0.0.1:5080
    ./scripts/e2e-test.py --api http://127.0.0.1:5080 --frontend http://127.0.0.1:3000
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

Zwei Spuren der neueren Abläufe lassen sich nicht ganz vermeiden (Näheres an
run_neue_funktionen): Der Einladungslink gehört dem vorhandenen Verein - sein Zustand
(aktiv/aus) wird wiederhergestellt, ein aktiver Link hat danach aber einen NEUEN Code.
Und ein Beitritt über den Link benachrichtigt die Demo-Trainer:in; diese Meldungen
werden als gelesen markiert.

Die Demo-Szenarien (DemoSzenarienSeeder) stören den Vergleich nicht: Sie laufen nur beim
Start des Servers, nicht während des Skripts, und das Skript fasst sie nicht an.

Am Ende vergleicht das Skript einen Vorher-/Nachher-Abzug und meldet jede
Abweichung.
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import date, timedelta

PREFIX = "e2e-"
PASSWORD = "E2eTest1234!"
ADMIN = ("admin@dogity.test", "Demo1234!")
# Die Demo-Trainer:in bekommt Benachrichtigungen, die ein Testlauf auslöst (Beitritt über den
# Einladungslink meldet an ALLE Vereinstrainer:innen). Das Skript markiert genau diese als gelesen.
DEMO_TRAINER = ("trainer@dogity.test", "Demo1234!")

# Zieladressen, gegen die geschrieben werden darf. Alles andere wird abgelehnt.
# Eine feste Liste statt einer "test kommt im Namen vor"-Regel: die Regel würde
# eine Produktivadresse durchlassen, in der zufällig "test" steckt.
ERLAUBTE_ZIELE = {
    "api-test.dogity.net",
    "localhost",
    "127.0.0.1",
    "::1",
}

# Frontend-Adressen, gegen die der (nur lesende) Kopfzeilen-Test laufen darf.
ERLAUBTE_FRONTENDS = {
    "test.dogity.net",
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
        # Anmeldungen sind auf 10 pro Minute und IP begrenzt (Program.cs,
        # Policy "auth"). Ein Durchlauf meldet ein halbes Dutzend Nutzer an,
        # teils mehrfach - ohne Zwischenspeicher läuft er ins Limit und meldet
        # dann Fehler, die keine sind.
        self._tokens: dict[str, str] = {}

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

    def login(self, email: str, password: str, force: bool = False) -> str | None:
        if not force and email in self._tokens:
            return self._tokens[email]
        status, body = self.call("POST", "/api/auth/login", {"email": email, "password": password})
        if status == 429 or status == 403:
            print(f"  {YELLOW}Anmeldung gedrosselt ({status}) - eine Minute warten…{OFF}")
            time.sleep(62)
            status, body = self.call("POST", "/api/auth/login", {"email": email, "password": password})
        if status != 200 or not isinstance(body, dict):
            return None
        self._tokens[email] = body["token"]
        return body["token"]

    def login_status(self, email: str, password: str) -> int:
        """
        Meldet an und gibt NUR den Status zurück - für Prüfungen, die den
        Anmeldevorgang selbst betreffen (gesperrter Nutzer, falsches Passwort).

        Mit derselben Wartezeit bei Drosselung wie login(): Ein 429 hier würde
        sonst als "abgewiesen" durchgehen und die Prüfung wertlos machen - sie
        soll ja belegen, dass die APP ablehnt, nicht der Zähler davor.
        """
        status, _ = self.call("POST", "/api/auth/login", {"email": email, "password": password})
        if status in (429, 403):
            print(f"  {YELLOW}Anmeldung gedrosselt ({status}) - eine Minute warten…{OFF}")
            time.sleep(62)
            status, _ = self.call("POST", "/api/auth/login", {"email": email, "password": password})
        return status

    def forget(self, email: str) -> None:
        """Zwischengespeicherten Token verwerfen - z.B. nach dem Löschen des Nutzers."""
        self._tokens.pop(email, None)

    def register(self, email: str, first: str, last: str) -> tuple[str, str] | None:
        """
        Legt einen Nutzer an oder meldet einen bestehenden an. (Token, UserId).

        Mit derselben Wartezeit bei Drosselung wie login(): Anlegen und Anmelden
        laufen über denselben Zähler, und ein Lauf kurz nach dem vorigen bringt
        ihn zum Überlaufen. Ohne das Warten liefert register() dann None, und der
        Lauf bricht mitten im Ablauf ab - was aussieht wie ein Fehler der App.
        """
        for versuch in (1, 2):
            status, body = self.call(
                "POST", "/api/auth/register",
                {"email": email, "password": PASSWORD, "firstName": first, "lastName": last},
            )
            if status in (200, 201):
                break
            status, body = self.call("POST", "/api/auth/login", {"email": email, "password": PASSWORD})
            if status == 200:
                break
            if status in (429, 403) and versuch == 1:
                print(f"  {YELLOW}Anmeldung gedrosselt ({status}) - eine Minute warten…{OFF}")
                time.sleep(62)
                continue
            return None

        if not isinstance(body, dict) or "token" not in body:
            return None
        self._tokens[email] = body["token"]
        return body["token"], body["userId"]


# --- Zustandsabzug ---------------------------------------------------------


def snapshot(api: Api, admin_token: str) -> dict:
    """Alles, was ein Testlauf verändern könnte - für den Vorher-/Nachher-Vergleich."""
    _, users = api.call("GET", "/api/admin/users?pageSize=200", token=admin_token)
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    _, waisen = api.call("GET", "/api/admin/orphaned-data", token=admin_token)
    state = {
        "users": sorted(u["email"] for u in users["users"]),
        "clubs": {},
        # Daten gelöschter Konten: Das Löschen der Testnutzer darf keine hinterlassen.
        "waisen": waisen if isinstance(waisen, dict) else {},
        # Ungelesene Meldungen der Demo-Trainer:in (None, wenn es sie hier nicht gibt).
        "trainer_ungelesen": _demo_trainer_ungelesen(api),
    }
    for club in clubs:
        _, detail = api.call("GET", f"/api/admin/clubs/{club['id']}", token=admin_token)
        state["clubs"][club["name"]] = {
            "trainers": sorted(t["email"] for t in detail.get("trainers", [])),
            "groups": sorted(g["name"] for g in detail.get("groups", [])),
        }
    return state


def _demo_trainer_ungelesen(api: Api) -> int | None:
    token = api.login(*DEMO_TRAINER)
    if not token:
        return None
    status, anzahl = api.call("GET", "/api/notifications/unread-count", token=token)
    return anzahl if status == 200 and isinstance(anzahl, int) else None


def diff_state(before: dict, after: dict) -> list[str]:
    problems = []
    if before.get("waisen") != after.get("waisen"):
        problems.append(f"verwaiste Daten: {before.get('waisen')} -> {after.get('waisen')}")
    if before.get("trainer_ungelesen") != after.get("trainer_ungelesen"):
        problems.append(f"ungelesene Meldungen der Demo-Trainer:in: "
                        f"{before.get('trainer_ungelesen')} -> {after.get('trainer_ungelesen')}")
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
    # Vor dem Löschen merken: Danach gibt es die Namen nicht mehr, die Meldungen der Demo-Trainer:in aber schon.
    namen = {f"{u['firstName']} {u['lastName']}".strip() for u in e2e_users} - {""}

    # Erst die Inhalte der Nutzer wegräumen (Gruppen, Hunde), dann die Nutzer.
    for user in e2e_users:
        token = api.login(user["email"], PASSWORD)
        if not token:
            continue
        for group in (api.call("GET", "/api/groups", token=token)[1] or []):
            if group["name"].startswith(PREFIX):
                # Künftige Termine verhindern das Löschen der Gruppe.
                if group.get("clubId"):
                    _sessions_der_gruppe_loeschen(api, token, group["clubId"], group["id"])
                api.call("DELETE", f"/api/groups/{group['id']}", token=token)
        for dog in (api.call("GET", "/api/dogs", token=token)[1] or []):
            api.call("DELETE", f"/api/dogs/{dog['id']}", token=token)
        # Aus allen Vereinen austreten, damit keine Mitgliedschaft zurückbleibt.
        for club in (api.call("GET", "/api/clubs", token=token)[1] or []):
            api.call("DELETE", f"/api/clubs/{club['id']}/membership", token=token)

    for user in e2e_users:
        status, _ = api.call("DELETE", f"/api/admin/users/{user['id']}", token=admin_token)
        api.forget(user["email"])
        if verbose:
            print(f"  {DIM}Nutzer entfernt: {user['email']} ({status}){OFF}")

    _demo_trainer_meldungen_lesen(api, verbose, namen)

    # Verwaiste e2e-Gruppen (falls ihr Trainer schon weg ist) und
    # Trainerzuweisungen ohne Nutzer.
    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    for club in clubs or []:
        _, detail = api.call("GET", f"/api/admin/clubs/{club['id']}", token=admin_token)
        for group in detail.get("groups", []):
            if group["name"].startswith(PREFIX) and verbose:
                print(f"  {YELLOW}Achtung: Gruppe '{group['name']}' konnte nicht entfernt werden{OFF}")

        # Das Löschen eines Nutzers räumt seine Vereinstrainer-Zeile nicht mit
        # weg. Bricht ein Lauf ab, nachdem er jemanden zum Vereinstrainer
        # gemacht hat, bleibt die Zeile für immer stehen und taucht in der
        # Trainerliste als "(unbekannt)" auf. Genau so ist eine solche Waise
        # auf der lokalen Datenbank entstanden.
        for trainer in detail.get("trainers", []):
            verwaist = trainer["email"] == "(unbekannt)"
            if verwaist or trainer["email"].startswith(PREFIX):
                api.call("DELETE", f"/api/admin/clubs/{club['id']}/trainers/{trainer['userId']}",
                         token=admin_token)
                if verbose:
                    grund = "verwaist" if verwaist else "e2e"
                    print(f"  {DIM}Vereinstrainer entfernt ({grund}): {trainer['email']}{OFF}")


def _demo_trainer_meldungen_lesen(api: Api, verbose: bool, namen: set[str]) -> None:
    """
    Benachrichtigungen lassen sich nicht löschen. Was ein Testlauf der Demo-Trainer:in
    gemeldet hat (erkennbar am vollen Namen eines Testnutzers), wird als gelesen
    markiert - so zählt ihr Glockenzähler wie vorher und die Meldung stört nicht.
    """
    token = api.login(*DEMO_TRAINER)
    if not token:
        return
    _, rows = api.call("GET", "/api/notifications", token=token)
    markiert = 0
    for n in rows if isinstance(rows, list) else []:
        if not n["isRead"] and any(name in n["message"] for name in namen):
            api.call("POST", f"/api/notifications/{n['id']}/read", token=token)
            markiert += 1
    if markiert and verbose:
        print(f"  {DIM}Meldungen der Demo-Trainer:in als gelesen markiert: {markiert}{OFF}")


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
    # Wer eingeladen wird, ist erst nach dem Annehmen Mitglied - und nur Mitglieder lassen sich betreuen.
    api.call("POST", f"/api/groups/{group_id}/invitation/accept", token=owner_token)

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
    api.call("POST", f"/api/groups/{group_id}/invitation/accept", token=owner_token)

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
    # Auch die Einladung zur weiteren Trainer:in gilt erst nach dem Annehmen.
    api.call("POST", f"/api/groups/{group_id}/co-trainer-invitation/accept", token=helper_token)
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
    api.call("POST", f"/api/groups/{cg_id}/invitation/accept", token=owner_token)
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
    api.call("POST", f"/api/groups/{grp['id']}/invitation/accept", token=owner_token)
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

    section("Geführter Erststart")
    erststart_token, _ = api.register(f"{PREFIX}erststart@dogity.test", "Neu", "Ling")
    status, stand = api.call("GET", "/api/onboarding/status", token=erststart_token)
    check(status == 200 and stand["hasDog"] is False and stand["isComplete"] is False,
          "frischer Nutzer: noch kein Hund", str(stand if status == 200 else status))
    check(stand["firstDogId"] is None, "ohne Hund kein Verweisziel")

    _, erststartHund = api.call("POST", "/api/dogs",
                               {"name": f"{PREFIX}Erstling", "breed": "Mix", "gender": 1}, erststart_token)
    _, stand = api.call("GET", "/api/onboarding/status", token=erststart_token)
    check(stand["hasDog"] and stand["firstDogId"] == erststartHund["id"],
          "der erste Hund trägt die Verweise", str(stand["firstDogId"]))
    check(stand["isComplete"] is False, "ein Hund allein schließt den Erststart nicht ab")

    api.call("POST", "/api/trainings", {
        "dogId": erststartHund["id"], "date": str(today), "durationMinutes": 20, "notes": None,
        "exercises": [{"exerciseId": None, "freeTextLabel": "Erste Übung", "rating": 4, "difficulty": 0,
                       "success": True, "notes": None, "trainingPlanItemId": None}],
    }, erststart_token)
    _, stand = api.call("GET", "/api/onboarding/status", token=erststart_token)
    check(stand["hasTraining"] and stand["isComplete"],
          "erstes Training schließt den Erststart ab", str(stand))

    # Zweiter Nutzer für den Vereinsweg: offene Anfrage ist kein Abschluss.
    verein_token, _ = api.register(f"{PREFIX}erststart2@dogity.test", "Verein", "Weg")
    api.call("POST", "/api/dogs", {"name": f"{PREFIX}Zweitling", "breed": "Mix", "gender": 1}, verein_token)
    api.call("POST", f"/api/clubs/{club_id}/join-requests", token=verein_token)
    _, stand = api.call("GET", "/api/onboarding/status", token=verein_token)
    check(stand["hasPendingClubRequest"] and not stand["hasClubMembership"],
          "offene Vereinsanfrage zählt als wartend, nicht als Mitgliedschaft", str(stand))
    check(stand["isComplete"] is False, "Warten auf Freigabe schließt nicht ab")

    check(api.call("POST", "/api/onboarding/dismiss", token=verein_token)[0] == 204,
          "Erststart wegklickbar")
    _, stand = api.call("GET", "/api/onboarding/status", token=verein_token)
    check(stand["isDismissed"], "und bleibt weggeklickt")
    check(api.call("GET", "/api/onboarding/status")[0] == 401, "ohne Anmeldung gesperrt")

    section("Verein: Trainer gehört dazu und nimmt auf")
    if club_id:
        # Eigenständig: die Trainerrolle wird hier gesetzt und am Ende wieder
        # entfernt. Der Abschnitt davor räumt seine eigene Zuweisung auf - sich
        # darauf zu verlassen, hat diese Prüfungen erst fälschlich rot gemacht.
        api.call("POST", f"/api/admin/clubs/{club_id}/trainers",
                 {"email": f"{PREFIX}sweeptrainer@dogity.test"}, admin_token)

        # Trainer:innen bekommen beim Zuweisen KEINE Mitgliedschaftszeile. Der
        # Erststart forderte sie deshalb auf, einem Verein beizutreten, den sie
        # leiten - und einer Gruppe, die sie führen.
        _, trainerStand = api.call("GET", "/api/onboarding/status", token=trainer_token)
        check(trainerStand["hasClubMembership"], "Vereinstrainer gilt als vereinszugehörig", str(trainerStand))
        check(not trainerStand["hasPendingClubRequest"], "und nicht als wartend")
        check(trainerStand["hasGroupMembership"], "Gruppenleiter gilt als gruppenzugehörig")

        aufnahme_token, _ = api.register(f"{PREFIX}aufgenommen@dogity.test", "Auf", "Nahme")
        status, _ = api.call("POST", f"/api/clubs/{club_id}/members",
                             {"email": f"{PREFIX}aufgenommen@dogity.test"}, trainer_token)
        check(status in (200, 204), "Trainer nimmt jemanden direkt auf", str(status))
        _, aufgenommen = api.call("GET", "/api/onboarding/status", token=aufnahme_token)
        check(aufgenommen["hasClubMembership"], "der Aufgenommene ist sofort Mitglied", str(aufgenommen))
        check(api.call("POST", f"/api/clubs/{club_id}/members",
                       {"email": f"{PREFIX}aufgenommen@dogity.test"}, trainer_token)[0] == 400,
              "zweimal aufnehmen wird abgewiesen")
        check(api.call("POST", f"/api/clubs/{club_id}/members",
                       {"email": f"{PREFIX}aufgenommen@dogity.test"}, owner_token)[0] == 404,
              "wer nicht Trainer dieses Vereins ist, darf niemanden aufnehmen")
        check(api.call("POST", f"/api/clubs/{club_id}/members",
                       {"email": "gibtsnicht@dogity.test"}, trainer_token)[0] == 400,
              "unbekannte E-Mail wird abgewiesen")

        api.call("DELETE", f"/api/admin/clubs/{club_id}/trainers/{trainer_id}", token=admin_token)

    section("Verfassung")
    # Verfassung als Zahl: die API überträgt Enums numerisch (siehe difficulty).
    MOTIVIERT, ABGELENKT, MUEDE, GESTRESST = 0, 2, 3, 4
    _, verfassungshund = api.call("POST", "/api/dogs",
                                  {"name": f"{PREFIX}Verfassung", "breed": "Mix", "gender": 1}, owner_token)
    vhund = verfassungshund["id"]

    def verfassungstraining(tage_zurueck: int, verfassung, bewertungen):
        return api.call("POST", "/api/trainings", {
            "dogId": vhund,
            "date": str(today - timedelta(days=tage_zurueck)),
            "durationMinutes": 30, "notes": None, "condition": verfassung,
            "exercises": [{"exerciseId": None, "freeTextLabel": "Übung", "rating": b, "difficulty": 0,
                           "success": b >= 3, "notes": None, "trainingPlanItemId": None} for b in bewertungen],
        }, owner_token)

    status, ersteEinheit = verfassungstraining(10, MOTIVIERT, [5, 5])
    check(status in (200, 201) and ersteEinheit["condition"] == MOTIVIERT,
          "Verfassung beim Anlegen speicherbar", str(status))
    _, zweiteAmSelbenTag = verfassungstraining(10, GESTRESST, [4])
    check(zweiteAmSelbenTag["condition"] == MOTIVIERT,
          "zweiter Eintrag desselben Tages überschreibt die Verfassung nicht",
          str(zweiteAmSelbenTag["condition"]))
    verfassungstraining(9, MUEDE, [3, 3])
    verfassungstraining(8, GESTRESST, [2])
    verfassungstraining(3, None, [4])

    status, verfassung = api.call("GET", f"/api/stats/dogs/{vhund}/condition", token=owner_token)
    check(status == 200 and len(verfassung["byCondition"]) == 3,
          "Auswertung nach Verfassung abrufbar", str(status))
    dichte = {d["precedingTrainingDays"]: d for d in verfassung["byPrecedingDays"]}
    check(sorted(dichte) == [0, 1, 2], "drei Gruppen nach Trainingstagen am Stück", str(sorted(dichte)))
    check(dichte[0]["avgRating"] > dichte[2]["avgRating"],
          "nach einer Pause fällt die Bewertung besser aus als am dritten Tag",
          f"{dichte[0]['avgRating']} vs {dichte[2]['avgRating']}")
    check(dichte[2]["tiredOrStressedShare"] == 1.0,
          "müde/gestresst am dritten Tag wird ausgewiesen", str(dichte[2]))
    # Vier Trainingstage (der zweite Eintrag am selben Tag wird verschmolzen),
    # drei davon mit Verfassung.
    check(verfassung["sessionsWithCondition"] == 3 and verfassung["sessionsTotal"] == 4,
          "Abdeckung wird ausgewiesen", str((verfassung["sessionsWithCondition"], verfassung["sessionsTotal"])))

    status, _ = api.call("PUT", f"/api/trainings/{ersteEinheit['id']}/context",
                         {"startTime": None, "latitude": None, "longitude": None,
                          "locationName": None, "condition": ABGELENKT}, owner_token)
    _, nachher = api.call("GET", f"/api/trainings?dogId={vhund}", token=owner_token)
    check(status == 200 and any(s["condition"] == ABGELENKT for s in nachher),
          "Verfassung nachträglich änderbar")
    api.call("PUT", f"/api/trainings/{ersteEinheit['id']}/context",
             {"startTime": None, "latitude": None, "longitude": None,
              "locationName": None, "condition": None}, owner_token)
    _, geleert = api.call("GET", f"/api/trainings?dogId={vhund}", token=owner_token)
    check(all(s["condition"] != ABGELENKT for s in geleert), "und wieder entfernbar")
    check(api.call("GET", f"/api/stats/dogs/{uuid.uuid4()}/condition", token=owner_token)[0] == 404,
          "fremder Hund ist nicht einsehbar")

    section("Sachkunde-Fragentrainer")
    status, kataloge = api.call("GET", "/api/sachkunde/catalogs")
    check(status == 200 and len(kataloge) >= 2, f"Fragenkataloge anonym lesbar ({len(kataloge) if status==200 else status})")
    erw = next((k for k in kataloge if k["code"] == "SWHV-BHVT-ERW"), None)
    check(erw is not None and erw["questionCount"] == 72 and len(erw["sections"]) == 5,
          "Erwachsenenkatalog: 72 Fragen in 5 Komplexen",
          str(erw and (erw["questionCount"], len(erw["sections"]))))
    status, fragen = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/questions?section=C")
    check(status == 200 and len(fragen) == 10, f"Komplex einzeln abrufbar ({len(fragen) if status==200 else status})")
    check(all(f["kind"] != "SingleChoice" or any(o["isCorrect"] for o in f["options"]) for f in fragen),
          "jede Auswahlfrage hat eine hinterlegte Lösung")
    check(api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/session")[0] == 401,
          "Lernstand ist ohne Anmeldung gesperrt")

    status, sitzung = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/session?limit=5", token=owner_token)
    check(status == 200 and len(sitzung["questions"]) == 5 and sitzung["progress"]["answered"] == 0,
          "Lernrunde wird geliefert", str(status))
    frage = sitzung["questions"][0]
    falsch = [o["id"] for o in frage["options"] if not o["isCorrect"]][:1]
    richtig = [o["id"] for o in frage["options"] if o["isCorrect"]]
    status, ergebnis = api.call("POST", f"/api/sachkunde/questions/{frage['id']}/answer",
                                {"selectedOptionIds": falsch}, owner_token)
    check(status == 200 and ergebnis["correct"] is False and ergebnis["box"] == 1,
          "falsche Antwort wird erkannt und setzt das Fach zurück", str(ergebnis))
    check(ergebnis.get("progress") is not None, "die Antwort bringt den Lernstand gleich mit")
    check(ergebnis["progress"]["correct"] == 0 and ergebnis["progress"]["inMistakes"] == 1,
          "falsch beantwortet zählt nicht als richtig", str(ergebnis["progress"]))
    _, fehler = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/session?mode=mistakes", token=owner_token)
    check([q["id"] for q in fehler["questions"]] == [frage["id"]], "Frage steht im Fehlerspeicher")
    _, ergebnis = api.call("POST", f"/api/sachkunde/questions/{frage['id']}/answer",
                           {"selectedOptionIds": richtig}, owner_token)
    check(ergebnis["correct"] is True and ergebnis["box"] == 2, "richtige Antwort hebt das Fach", str(ergebnis))
    # Der Zähler muss sich sofort bewegen: "gekonnt" (Fach 4) braucht drei
    # richtige Antworten an verschiedenen Tagen und stünde tagelang auf 0.
    check(ergebnis["progress"]["correct"] == 1 and ergebnis["progress"]["mastered"] == 0,
          "richtig zählt sofort, sicher sitzt noch nichts", str(ergebnis["progress"]))
    _, fehler = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/session?mode=mistakes", token=owner_token)
    check(fehler["questions"] == [], "und räumt den Fehlerspeicher")
    check(api.call("POST", f"/api/sachkunde/questions/{frage['id']}/answer",
                   {"selectedOptionIds": []}, owner_token)[0] == 400, "leere Auswahl wird abgewiesen")

    # Zuordnungen werden zugeordnet und vom Server geprüft; nur die offenen
    # Freitextfragen bleiben Selbsteinschätzung.
    _, alle_fragen = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/questions")
    zuordnungen = [f for f in alle_fragen if f["kind"] == "Assignment"]
    check(len(zuordnungen) == 3 and all(f["terms"] and f["keys"] for f in zuordnungen),
          "alle drei Zuordnungen haben Begriffe und Schlüssel",
          str([(f["number"], len(f["terms"]), len(f["keys"])) for f in zuordnungen]))
    check(any(f["imageName"] for f in zuordnungen), "die Bildfrage bringt ihre Abbildung mit")
    check(all(len({t["solutionKey"] for t in f["terms"]}) == len(f["terms"]) for f in zuordnungen),
          "jeder Schlüssel kommt in einer Zuordnung genau einmal vor")

    zuo = zuordnungen[0]
    richtige_zuordnung = {t["id"]: t["solutionKey"] for t in zuo["terms"]}
    vertauscht = dict(richtige_zuordnung)
    erste, zweite = list(vertauscht)[:2]
    vertauscht[erste], vertauscht[zweite] = vertauscht[zweite], vertauscht[erste]
    status, r = api.call("POST", f"/api/sachkunde/questions/{zuo['id']}/answer",
                         {"assignments": vertauscht}, owner_token)
    check(status == 200 and r["correct"] is False and sum(1 for v in r["termResults"].values() if not v) == 2,
          "vertauschte Zuordnung ist falsch, die zwei Fehler sind benannt", str(status))
    status, r = api.call("POST", f"/api/sachkunde/questions/{zuo['id']}/answer",
                         {"assignments": richtige_zuordnung}, owner_token)
    check(status == 200 and r["correct"] is True and all(r["termResults"].values()),
          "vollständig richtige Zuordnung wird angenommen", str(status))
    check(api.call("POST", f"/api/sachkunde/questions/{zuo['id']}/answer",
                   {"assignments": {erste: richtige_zuordnung[erste]}}, owner_token)[0] == 400,
          "unvollständige Zuordnung wird abgewiesen")
    check(api.call("POST", f"/api/sachkunde/questions/{zuo['id']}/answer",
                   {"selfAssessedCorrect": True}, owner_token)[0] == 400,
          "Selbsteinschätzung reicht bei einer Zuordnung nicht")

    freitext = next(f for f in alle_fragen if f["kind"] == "FreeText")
    check(bool(freitext["sampleSolution"]), "Freitextfrage hat eine Musterlösung")
    check(api.call("POST", f"/api/sachkunde/questions/{freitext['id']}/answer",
                   {"selectedOptionIds": []}, owner_token)[0] == 400,
          "Freitext ohne Selbsteinschätzung abgewiesen")
    check(api.call("POST", f"/api/sachkunde/questions/{freitext['id']}/answer",
                   {"selfAssessedCorrect": True}, owner_token)[0] == 200,
          "Freitext per Selbsteinschätzung angenommen")

    status, stand = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/progress", token=owner_token)
    check(status == 200 and stand["answered"] == 3 and stand["total"] == 72,
          "Lernstand zählt die beantworteten Fragen", str(status))
    check(api.call("POST", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/reset", token=owner_token)[0] == 204,
          "von vorne anfangen wird angenommen")
    _, stand = api.call("GET", "/api/sachkunde/catalogs/SWHV-BHVT-ERW/progress", token=owner_token)
    check(stand["answered"] == 0 and stand["correct"] == 0 and stand["mastered"] == 0
          and stand["inMistakes"] == 0,
          "nach dem Neustart ist der Lernstand leer", str(stand))
    check(api.call("GET", "/api/sachkunde/catalogs/GIBTSNICHT/session", token=owner_token)[0] == 404,
          "unbekannter Katalog ist 404")

    section("Sachkunde-Verwaltung (Admin)")
    status, verwaltung = api.call("GET", "/api/admin/sachkunde/questions", token=admin_token)
    check(status == 200 and len(verwaltung) == 112, f"alle Fragen abrufbar ({len(verwaltung) if status==200 else status})")
    check(all(o["kind"] in ("Answer", "Term", "Label") for f in verwaltung for o in f["options"]),
          "jede Zeile trägt ihre Rolle")
    check(api.call("GET", "/api/admin/sachkunde/questions")[0] == 401, "ohne Anmeldung gesperrt")
    check(api.call("GET", "/api/admin/sachkunde/questions", token=owner_token)[0] == 403,
          "Nicht-Admin wird abgewiesen")
    status, eingegrenzt = api.call("GET", "/api/admin/sachkunde/questions?catalog=SWHV-BHVT-ERW&section=C",
                                   token=admin_token)
    check(status == 200 and len(eingegrenzt) == 10, "Filter nach Katalog und Komplex")
    status, gesucht = api.call("GET", "/api/admin/sachkunde/questions?search=Baurecht", token=admin_token)
    check(status == 200 and any(f["number"] == "C1" for f in gesucht), "Suche greift bis in die Antworttexte")

    # Bewusst mit UNVERÄNDERTEM Text speichern: der Smoketest darf keine
    # Fragentexte auf test hinterlassen. Geprüft wird die Marke, nicht der Inhalt.
    frage = next(f for f in verwaltung if f["number"] == "C1")
    unveraendert = {
        "text": frage["text"],
        "sampleSolution": frage["sampleSolution"],
        "options": [{"id": o["id"], "kind": o["kind"], "text": o["text"], "isCorrect": o["isCorrect"],
                     "matchKey": o["matchKey"], "imageName": o["imageName"]} for o in frage["options"]],
    }
    status, gespeichert = api.call("PUT", f"/api/admin/sachkunde/questions/{frage['id']}",
                                   unveraendert, admin_token)
    check(status == 200 and gespeichert["editedAt"] is not None,
          "Speichern markiert die Frage als von Hand bearbeitet", str(status))
    check(gespeichert["text"] == frage["text"], "und lässt den Text unangetastet")

    ohne_loesung = dict(unveraendert)
    ohne_loesung["options"] = [{**o, "isCorrect": False} for o in unveraendert["options"]]
    check(api.call("PUT", f"/api/admin/sachkunde/questions/{frage['id']}", ohne_loesung, admin_token)[0] == 400,
          "Frage ohne richtige Antwort wird abgewiesen")
    leer = dict(unveraendert, text="   ")
    check(api.call("PUT", f"/api/admin/sachkunde/questions/{frage['id']}", leer, admin_token)[0] == 400,
          "leere Fragestellung wird abgewiesen")

    check(api.call("POST", f"/api/admin/sachkunde/questions/{frage['id']}/revert", token=admin_token)[0] == 204,
          "Bearbeitungsmarke zurücknehmbar")
    status, danach = api.call("GET", "/api/admin/sachkunde/questions?search=Rechtsgebiet", token=admin_token)
    check(status == 200 and all(f["editedAt"] is None for f in danach), "Marke ist wieder weg")

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
    check(api.login_status(f"{PREFIX}sweep@dogity.test", PASSWORD) != 200,
          "gesperrter Nutzer kommt nicht mehr rein")
    check(api.call("POST", f"/api/admin/users/{owner_id}/unlock", token=admin_token)[0] == 204, "und wieder entsperrbar")
    check(api.login_status(f"{PREFIX}sweep@dogity.test", PASSWORD) == 200,
          "danach wieder anmeldbar")
    check(api.login_status(f"{PREFIX}sweep@dogity.test", "falsch") in (400, 401),
          "falsches Passwort wird abgewiesen")


# --- Die neueren Funktionen --------------------------------------------------

# Das öffentliche Anmeldeformular der Gruppen ist auf 10 Schreibzugriffe je
# Minute und IP begrenzt (Program.cs, Policy "anmeldung"). Der Ablauf unten
# schickt einige ab; ohne Bremse liefe er ins Limit und meldete Fehler, die
# keine sind. Gezählt wird hier, gewartet erst kurz vor der Grenze.
_anonyme_schreibzugriffe: list[float] = []
ANONYM_LIMIT_JE_MINUTE = 10


def anonym_schreiben(api: Api, path: str, body: dict):
    jetzt = time.time()
    _anonyme_schreibzugriffe[:] = [t for t in _anonyme_schreibzugriffe if jetzt - t < 60]
    if len(_anonyme_schreibzugriffe) >= ANONYM_LIMIT_JE_MINUTE - 1:
        warte = 61 - (jetzt - _anonyme_schreibzugriffe[0])
        print(f"  {YELLOW}Anmeldeformular fast am Limit - {warte:.0f} s warten…{OFF}")
        time.sleep(max(warte, 1))
        _anonyme_schreibzugriffe.clear()
    _anonyme_schreibzugriffe.append(time.time())
    return api.call("POST", path, body)


def benachrichtigungen(api: Api, token: str) -> list[dict]:
    status, rows = api.call("GET", "/api/notifications", token=token)
    return rows if status == 200 and isinstance(rows, list) else []


def mit_text(rows: list[dict], teil: str) -> list[dict]:
    return [r for r in rows if teil in r["message"]]


def run_neue_funktionen(api: Api, admin_token: str, frontend: str | None) -> None:
    """
    Dritter Teil: Einladungslink, Prüfungsziele mit Ergebnis, Gruppentermine,
    Trainer-Feedback, Gruppen-Anmeldungen und die Admin-Kennzahlen.

    Eigene Nutzer, eigene Gruppe, alles mit Präfix. Zwei Spuren lassen sich
    nicht vermeiden und werden eigens behandelt:
    - Der Verein ist der vorhandene (Vereine lassen sich nicht löschen). Der
      Einladungslink gehört ihm; "neuer Link entwertet den alten" ist nur an
      ihm zu zeigen. Am Ende wird der Zustand von vorher nachgebaut: war der
      Link aktiv, gibt es einen neuen aktiven (der alte Code ist ja gerade
      entwertet), war er aus, bleibt er aus.
    - Ein Beitritt über den Link benachrichtigt ALLE Vereinstrainer:innen -
      auch die Demo-Trainer:in (so schon die älteren Abläufe oben). Diese
      Meldungen werden in cleanup() als gelesen markiert (löschen lässt sich
      eine Benachrichtigung nicht).
    """
    today = date.today()
    owner_email = f"{PREFIX}nf-besitzer@dogity.test"
    trainer_email = f"{PREFIX}nf-trainer@dogity.test"
    member_email = f"{PREFIX}nf-mitglied@dogity.test"

    section("Neue Funktionen - Aufbau")
    owner_token, owner_id = api.register(owner_email, "Olga", "E2E")
    trainer_token, trainer_id = api.register(trainer_email, "Tilo", "E2E")
    member_token, member_id = api.register(member_email, "Nora", "E2E")
    if not check(all([owner_token, trainer_token, member_token]), "drei Wegwerf-Nutzer angelegt"):
        return

    _, clubs = api.call("GET", "/api/admin/clubs", token=admin_token)
    if not check(bool(clubs), "Verein vorhanden"):
        return
    club_id, club_name = clubs[0]["id"], clubs[0]["name"]
    api.call("POST", f"/api/admin/clubs/{club_id}/trainers", {"email": trainer_email}, admin_token)

    _, stats_vorher = api.call("GET", "/api/admin/stats", token=admin_token)
    link_vorher, _ = api.call("GET", f"/api/clubs/{club_id}/invite-link", token=trainer_token)
    war_aktiv = link_vorher == 200

    try:
        _ablauf_einladungslink(api, admin_token, club_id, club_name, owner_token, trainer_token,
                               member_token, member_id, stats_vorher, frontend)
    finally:
        # Zustand des Links wie vorgefunden (siehe Docstring).
        if war_aktiv:
            api.call("POST", f"/api/clubs/{club_id}/invite-link", token=trainer_token)
        else:
            api.call("DELETE", f"/api/clubs/{club_id}/invite-link", token=trainer_token)

    section("Neue Funktionen - Gruppe und Mitglieder")
    status, group = api.call("POST", "/api/groups", {
        "name": f"{PREFIX}nf-gruppe", "description": None, "clubId": club_id}, trainer_token)
    if not check(status in (200, 201), "Vereinsgruppe angelegt", str(group)):
        return
    group_id = group["id"]
    for email, token in ((owner_email, owner_token), (member_email, member_token)):
        api.call("POST", f"/api/groups/{group_id}/members", {"email": email}, trainer_token)
        status, _ = api.call("POST", f"/api/groups/{group_id}/invitation/accept", token=token)
        check(status == 204, f"{email.split('@')[0]} nimmt die Einladung an", str(status))

    status, dog = api.call("POST", "/api/dogs", {"name": f"{PREFIX}Nuri", "breed": "Testhund", "gender": 1}, owner_token)
    check(status in (200, 201), "Hund angelegt", str(dog))
    dog_id = dog["id"]

    try:
        _ablauf_pruefungsziel(api, owner_token, member_token, dog_id, today)
        _ablauf_termine(api, club_id, group_id, owner_token, member_token, trainer_token, trainer_id, today)
        _ablauf_feedback(api, group_id, dog_id, owner_id, owner_token, member_token, trainer_token)
        _ablauf_anmeldungen(api, group_id, club_name, owner_token, trainer_token, today, frontend)
    finally:
        # Die Gruppe verlässt der Ablauf nicht ohne Not stehend: Sitzungen weg, dann die Gruppe
        # (und mit ihr die Anmeldungen). cleanup() erledigt dasselbe nach einem Abbruch.
        _sessions_der_gruppe_loeschen(api, trainer_token, club_id, group_id)
        api.call("DELETE", f"/api/groups/{group_id}", token=trainer_token)
        api.call("DELETE", f"/api/admin/clubs/{club_id}/trainers/{trainer_id}", token=admin_token)

    _ablauf_admin(api, admin_token, owner_token, stats_vorher)


def _sessions_der_gruppe_loeschen(api: Api, trainer_token: str, club_id: str, group_id: str) -> None:
    """Künftige Termine verhindern das Löschen der Gruppe (GroupService.DeleteGroupAsync)."""
    von, bis = date.today() - timedelta(days=1), date.today() + timedelta(days=400)
    status, termine = api.call(
        "GET", f"/api/group-training/schedule/clubs/{club_id}?from={von}&to={bis}&groupId={group_id}",
        token=trainer_token)
    if status != 200:
        return
    for termin in termine:
        api.call("DELETE", f"/api/group-training/schedule/sessions/{termin['id']}", token=trainer_token)


# ---------------------------------------------------------------- Einladungslink


def _ablauf_einladungslink(api, admin_token, club_id, club_name, owner_token, trainer_token,
                           member_token, member_id, stats_vorher, frontend) -> None:
    section("Einladungslink des Vereins")
    link = f"/api/clubs/{club_id}/invite-link"

    check(api.call("POST", link, token=owner_token)[0] == 404, "Nicht-Trainer:in kann keinen Link erzeugen")
    check(api.call("GET", link, token=owner_token)[0] == 404, "und sieht ihn auch nicht")

    status, neu = api.call("POST", link, token=trainer_token)
    check(status == 200 and len(neu["code"]) == 22, "Trainer:in erzeugt einen Link", str(neu))
    code1 = neu["code"]

    status, vorschau = api.call("GET", f"/api/clubs/invite/{code1}")
    check(status == 200 and vorschau == {"clubName": club_name},
          "Vorschau ohne Anmeldung nennt nur den Vereinsnamen", str(vorschau))
    check(api.call("GET", f"/api/clubs/invite/{uuid.uuid4().hex[:22]}")[0] == 404, "falscher Code (richtige Form) -> 404")
    check(api.call("GET", "/api/clubs/invite/kurz")[0] == 404, "Code in falscher Form -> 404")

    # Beitritt per Link
    status, beitritt = api.call("POST", f"/api/clubs/invite/{code1}/join", token=member_token)
    check(status == 200 and beitritt["status"] == 0 and beitritt["clubName"] == club_name,
          "Beitritt per Link legt eine offene Anfrage an", str(beitritt))
    status, anfragen = api.call("GET", f"/api/clubs/{club_id}/join-requests", token=trainer_token)
    meine = [a for a in anfragen if a["userId"] == member_id] if status == 200 else []
    check(len(meine) == 1 and meine[0]["source"] == 1, "Trainer:in sieht sie mit Quelle Einladungslink", str(meine))

    meldungen = mit_text(benachrichtigungen(api, trainer_token), "Nora E2E möchte dem Verein")
    check(len(meldungen) == 1, "Trainer:in wird benachrichtigt", str(len(meldungen)))

    status, nochmal = api.call("POST", f"/api/clubs/invite/{code1}/join", token=member_token)
    check(status == 200 and nochmal["id"] == beitritt["id"], "zweiter Beitritt ist idempotent (dieselbe Anfrage)", str(nochmal))
    check(len(mit_text(benachrichtigungen(api, trainer_token), "Nora E2E möchte dem Verein")) == 1,
          "und meldet nichts ein zweites Mal")
    check(api.call("POST", f"/api/clubs/invite/{code1}/join", token=trainer_token)[0] == 400,
          "Vereinstrainer:in kann sich nicht selbst anfragen")
    check(api.call("POST", f"/api/clubs/invite/{code1}/join")[0] == 401, "Beitritt braucht eine Anmeldung")

    _, stats_nachher = api.call("GET", "/api/admin/stats", token=admin_token)
    check(stats_nachher["last30Days"]["viaClubLink"] >= stats_vorher["last30Days"]["viaClubLink"] + 1,
          "Kennzahl 'über Einladungslink' zählt den Beitritt",
          f"{stats_vorher['last30Days']['viaClubLink']} -> {stats_nachher['last30Days']['viaClubLink']}")

    # Neuer Link entwertet den alten
    status, zweiter = api.call("POST", link, token=trainer_token)
    code2 = zweiter["code"]
    check(status == 200 and code2 != code1, "neuer Link hat einen anderen Code")
    check(api.call("GET", f"/api/clubs/invite/{code1}")[0] == 404, "der alte Link ist entwertet (Vorschau 404)")
    check(api.call("POST", f"/api/clubs/invite/{code1}/join", token=owner_token)[0] == 404, "und trägt nicht mehr (Beitritt 404)")
    check(api.call("GET", f"/api/clubs/invite/{code2}")[0] == 200, "der neue Link gilt")
    status, aktuell = api.call("GET", link, token=trainer_token)
    check(status == 200 and aktuell["code"] == code2, "Trainer:in sieht den neuen Code")

    if frontend:
        _robots_header(frontend, f"/v/{code2}", "Einladungsseite /v/")

    # Abschalten
    check(api.call("DELETE", link, token=owner_token)[0] == 404, "Nicht-Trainer:in kann den Link nicht abschalten")
    check(api.call("DELETE", link, token=trainer_token)[0] == 204, "Trainer:in schaltet den Link ab")
    check(api.call("GET", link, token=trainer_token)[0] == 204, "es gibt danach keinen Code mehr")
    check(api.call("GET", f"/api/clubs/invite/{code2}")[0] == 404, "Vorschau des abgeschalteten Links -> 404")
    check(api.call("POST", f"/api/clubs/invite/{code2}/join", token=owner_token)[0] == 404, "Beitritt darüber -> 404")

    # Die Anfrage von Nora bleibt zurück; cleanup() lässt sie über den Austritt/das Löschen des Kontos verschwinden.


def _robots_header(frontend: str, path: str, was: str) -> None:
    req = urllib.request.Request(frontend.rstrip("/") + path, headers={"User-Agent": "dogity-e2e"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            tag = resp.headers.get("X-Robots-Tag", "")
    except urllib.error.HTTPError as e:
        tag = e.headers.get("X-Robots-Tag", "")
    except urllib.error.URLError as e:
        check(False, f"{was}: Frontend erreichbar", str(e))
        return
    check("noindex" in tag, f"{was} trägt X-Robots-Tag noindex", f"war '{tag}'")


# ---------------------------------------------------------------- Prüfungsziel


def _ablauf_pruefungsziel(api, owner_token, fremd_token, dog_id, today) -> None:
    section("Prüfungsziel: Ergebnis, Verschieben, Folgestufen")
    _, sports = api.call("GET", "/api/sports")
    upr = next(s for s in sports if s["code"] == "UPR")
    bh = next(s for s in sports if s["code"] == "BH")
    _, upr_regs = api.call("GET", f"/api/sports/{upr['id']}/regulations")
    _, bh_regs = api.call("GET", f"/api/sports/{bh['id']}/regulations")
    upr1 = next(r for r in upr_regs if r["name"] == "FCI-UPr 1")
    bh_reg = next(r for r in bh_regs if r["name"] == "BH")

    def anlegen(sport, regulation, tage):
        return api.call("POST", "/api/goals", {
            "dogId": dog_id, "sportId": sport["id"], "regulationId": regulation["id"],
            "targetDate": str(today + timedelta(days=tage)), "notes": f"{PREFIX}Ziel", "isCustom": False,
        }, owner_token)

    status, ziel = anlegen(upr, upr1, 30)
    check(status in (200, 201) and ziel["status"] == 0 and ziel["maxPoints"], "Ziel FCI-UPr 1 mit Höchstpunktzahl angelegt", str(ziel))
    gid = ziel["id"]
    hoechst = ziel["maxPoints"]

    # nicht bestanden: Ziel läuft mit neuem Datum weiter, kein Ergebnis
    check(api.call("POST", f"/api/goals/{gid}/complete", {"passed": False}, owner_token)[0] == 400,
          "nicht bestanden ohne neues Datum -> 400")
    check(api.call("POST", f"/api/goals/{gid}/complete",
                   {"passed": False, "newTargetDate": str(today - timedelta(days=1))}, owner_token)[0] == 400,
          "nicht bestanden mit Datum in der Vergangenheit -> 400")
    neues_datum = today + timedelta(days=45)
    status, verschoben = api.call("POST", f"/api/goals/{gid}/complete",
                                  {"passed": False, "newTargetDate": str(neues_datum)}, owner_token)
    check(status == 200 and verschoben["status"] == 0 and verschoben["targetDate"] == str(neues_datum)
          and verschoben["examScore"] is None and verschoben["examDate"] is None,
          "nicht bestanden: Ziel bleibt aktiv, neues Datum, kein Ergebnis", str(verschoben))

    # bestanden: Prüfung der Eingaben
    zu_hoch = hoechst + 1
    status, body = api.call("POST", f"/api/goals/{gid}/complete",
                            {"passed": True, "examDate": str(today), "score": zu_hoch}, owner_token)
    check(status == 400 and str(hoechst) in json.dumps(body, ensure_ascii=False),
          f"zu hohe Punktzahl ({zu_hoch}) wird abgelehnt, Meldung nennt {hoechst}", str(body))
    check(api.call("POST", f"/api/goals/{gid}/complete",
                   {"passed": True, "examDate": str(today + timedelta(days=5)), "score": 80}, owner_token)[0] == 400,
          "Prüfungstag in der Zukunft wird abgelehnt")
    check(api.call("POST", f"/api/goals/{gid}/complete", {"passed": True, "score": 80}, owner_token)[0] == 400,
          "ohne Prüfungstag -> 400")
    check(api.call("POST", f"/api/goals/{gid}/complete",
                   {"passed": True, "examDate": str(today), "score": 80}, fremd_token)[0] == 404,
          "fremde Person kann das Ziel nicht abschließen (404)")

    punkte = hoechst - 14
    status, erreicht = api.call("POST", f"/api/goals/{gid}/complete",
                                {"passed": True, "examDate": str(today), "score": punkte, "note": "Klettersprung gezögert"},
                                owner_token)
    check(status == 200 and erreicht["status"] == 1 and erreicht["examScore"] == punkte
          and erreicht["examDate"] == str(today) and erreicht["examNote"] == "Klettersprung gezögert",
          "bestanden mit Punkten: Ziel erreicht, Ergebnis gespeichert", str(erreicht))
    namen = [n["name"] for n in (erreicht.get("nextStages") or [])]
    check("FCI-UPr 2" in namen, "Folgestufe FCI-UPr 2 wird angeboten", str(namen))
    check(api.call("POST", f"/api/goals/{gid}/complete",
                   {"passed": True, "examDate": str(today), "score": 50}, owner_token)[0] == 400,
          "ein erreichtes Ziel lässt sich nicht noch einmal abschließen")

    # Ergebnis nachtragen
    status, korrigiert = api.call("PUT", f"/api/goals/{gid}/exam-result",
                                  {"examDate": str(today - timedelta(days=1)), "score": hoechst - 5, "note": None}, owner_token)
    check(status == 200 and korrigiert["examScore"] == hoechst - 5 and korrigiert["examNote"] is None
          and korrigiert["examDate"] == str(today - timedelta(days=1)),
          "Ergebnis lässt sich korrigieren", str(korrigiert))
    check(api.call("PUT", f"/api/goals/{gid}/exam-result",
                   {"examDate": str(today), "score": zu_hoch, "note": None}, owner_token)[0] == 400,
          "Korrektur mit zu hoher Punktzahl -> 400")
    check(api.call("PUT", f"/api/goals/{gid}/exam-result",
                   {"examDate": str(today), "score": -1, "note": None}, owner_token)[0] == 400,
          "negative Punktzahl -> 400")
    check(api.call("PUT", f"/api/goals/{gid}/exam-result",
                   {"examDate": str(today), "score": 50, "note": None}, fremd_token)[0] == 404,
          "fremde Person kann kein Ergebnis eintragen (404)")

    # Prüfung ohne Punkte (BH): bestanden genügt
    status, bh_ziel = anlegen(bh, bh_reg, 20)
    status, bh_fertig = api.call("POST", f"/api/goals/{bh_ziel['id']}/complete",
                                 {"passed": True, "examDate": str(today)}, owner_token)
    check(status == 200 and bh_fertig["status"] == 1 and bh_fertig["examScore"] is None and bh_fertig["examDate"] == str(today),
          "BH ohne Punkte: bestanden mit Prüfungstag, ohne Punktzahl", str(bh_fertig))
    if bh_fertig.get("maxPoints"):
        status, zu_viel = api.call("PUT", f"/api/goals/{bh_ziel['id']}/exam-result",
                                   {"examDate": str(today), "score": bh_fertig["maxPoints"] + 1, "note": None}, owner_token)
        check(status == 400, "BH: Punktzahl über dem Höchstwert der Ordnung -> 400", str(zu_viel))

    status, liste = api.call("GET", f"/api/goals?dogId={dog_id}", token=owner_token)
    check(status == 200 and sum(1 for g in liste if g["status"] == 1) == 2, "beide Ziele stehen als erreicht in der Hundeliste")


# ---------------------------------------------------------------- Gruppentermine


def _utc(tage: int, stunde: int = 18) -> str:
    start = (date.today() + timedelta(days=tage))
    return f"{start}T{stunde:02d}:00:00Z"


def _ablauf_termine(api, club_id, group_id, owner_token, member_token, trainer_token, trainer_id, today) -> None:
    section("Gruppentermine: Zu- und Absagen")
    termin = "/api/group-training/schedule"

    def anlegen(tage):
        return api.call("POST", f"{termin}/clubs/{club_id}/sessions", {
            "groupId": group_id, "category": 0, "startsAt": _utc(tage), "durationMinutes": 60,
            "location": f"{PREFIX}Platz", "notes": None, "trainerUserIds": [trainer_id],
            "items": [{"freeText": f"{PREFIX}Übung"}],
        }, trainer_token)

    status, s1 = anlegen(3)
    check(status == 200 and s1["status"] == 0 and s1["attendingCount"] == 0, "erster Termin angelegt", str(s1))
    status, s2 = anlegen(4)
    check(status == 200, "zweiter Termin angelegt", str(s2))
    sid1, sid2 = s1["id"], s2["id"]

    status, meine = api.call("GET", f"{termin}/mine?from={today}", token=owner_token)
    check(status == 200 and any(t["id"] == sid1 for t in meine), "Mitglied sieht die Termine seiner Gruppe", str(status))
    check(api.call("PUT", f"{termin}/sessions/{sid1}/response", {"attending": True}, trainer_token)[0] == 404,
          "wer nicht Gruppenmitglied ist, kann nicht zusagen (404)")

    # Zusage und Absage
    status, nach_zusage = api.call("PUT", f"{termin}/sessions/{sid1}/response", {"attending": True}, owner_token)
    check(status == 200 and nach_zusage["myResponse"] is True and nach_zusage["attendingCount"] == 1,
          "Zusage wird gezählt", str(nach_zusage))
    check(not mit_text(benachrichtigungen(api, trainer_token), "Olga hat für"), "Zusagen lösen keine Benachrichtigung aus")

    status, nach_absage = api.call("PUT", f"{termin}/sessions/{sid1}/response", {"attending": False}, member_token)
    check(status == 200 and nach_absage["myResponse"] is False and nach_absage["decliningCount"] == 1,
          "Absage wird gezählt", str(nach_absage))
    absagen = mit_text(benachrichtigungen(api, trainer_token), f"Nora hat für {PREFIX}nf-gruppe am")
    check(len(absagen) == 1 and absagen[0]["linkPath"] == "/trainer/schedule",
          "Trainer:in erhält die Absage mit Link auf die Terminübersicht", str(absagen))
    api.call("PUT", f"{termin}/sessions/{sid1}/response", {"attending": False}, member_token)
    check(len(mit_text(benachrichtigungen(api, trainer_token), f"Nora hat für {PREFIX}nf-gruppe am")) == 1,
          "dieselbe Absage noch einmal meldet nichts Neues")

    # Zählungen ohne und mit Namen
    status, als_mitglied = api.call("GET", f"{termin}/mine?from={today}", token=owner_token)
    s1_mitglied = next(t for t in als_mitglied if t["id"] == sid1)
    check(s1_mitglied["attendingCount"] == 1 and s1_mitglied["decliningCount"] == 1
          and s1_mitglied["responses"] == [] and s1_mitglied["openCount"] == 0,
          "Mitglied sieht nur Zahlen, keine Namen", str(s1_mitglied))
    von, bis = today, today + timedelta(days=10)
    status, als_trainer = api.call("GET", f"{termin}/clubs/{club_id}?from={von}&to={bis}&groupId={group_id}", token=trainer_token)
    t1 = next(t for t in als_trainer if t["id"] == sid1)
    t2 = next(t for t in als_trainer if t["id"] == sid2)
    namen = sorted((r["firstName"], r["isAttending"]) for r in t1["responses"])
    check(namen == [("Nora", False), ("Olga", True)], "Trainer:in sieht die Namen zu Zu- und Absagen", str(namen))
    check(t2["openCount"] == 2 and t2["attendingCount"] == 0, "und für den unbeantworteten Termin zwei Offene", str(t2))

    # Termin absagen benachrichtigt die Zugesagten
    check(api.call("POST", f"{termin}/sessions/{sid1}/cancel", token=owner_token)[0] == 404,
          "Mitglied kann keinen Termin absagen")
    check(api.call("POST", f"{termin}/sessions/{sid1}/cancel", token=trainer_token)[0] == 204, "Trainer:in sagt den Termin ab")
    fallen_aus = mit_text(benachrichtigungen(api, owner_token), f"Das Training {PREFIX}nf-gruppe am")
    check(len(fallen_aus) == 1 and "fällt aus" in fallen_aus[0]["message"], "wer zugesagt hatte, erfährt es", str(fallen_aus))
    check(not mit_text(benachrichtigungen(api, member_token), f"Das Training {PREFIX}nf-gruppe am"),
          "wer abgesagt hatte, bekommt nichts")
    check(api.call("POST", f"{termin}/sessions/{sid1}/cancel", token=trainer_token)[0] == 204, "nochmal absagen ist erlaubt")
    check(len(mit_text(benachrichtigungen(api, owner_token), f"Das Training {PREFIX}nf-gruppe am")) == 1,
          "und meldet nichts ein zweites Mal")
    check(api.call("PUT", f"{termin}/sessions/{sid1}/response", {"attending": True}, member_token)[0] == 400,
          "auf einen abgesagten Termin lässt sich nicht zusagen")

    # Löschen
    check(api.call("DELETE", f"{termin}/sessions/{sid1}", token=owner_token)[0] == 404, "Mitglied kann keinen Termin löschen")
    check(api.call("DELETE", f"{termin}/sessions/{sid1}", token=trainer_token)[0] == 204, "Trainer:in löscht den abgesagten Termin")
    check(api.call("DELETE", f"{termin}/sessions/{sid2}", token=trainer_token)[0] == 204, "und den zweiten")
    _, rest = api.call("GET", f"{termin}/clubs/{club_id}?from={von}&to={bis}&groupId={group_id}", token=trainer_token)
    check(rest == [], "danach steht nichts mehr im Kalender der Gruppe", str(rest))


# ---------------------------------------------------------------- Feedback


def _ablauf_feedback(api, group_id, dog_id, owner_id, owner_token, fremd_token, trainer_token) -> None:
    section("Trainer-Feedback: Link, Reaktion, Rückfrage")
    today = date.today()
    check(api.call("POST", f"/api/groups/{group_id}/trainer-assignments",
                   {"memberId": owner_id, "dogId": dog_id}, trainer_token)[0] == 204, "Trainer:in betreut den Hund")

    _, sports = api.call("GET", "/api/sports", token=owner_token)
    bh = next(s for s in sports if s["code"] == "BH")
    _, exercises = api.call("GET", f"/api/sports/{bh['id']}/exercises", token=owner_token)
    uebung = next(e for e in exercises if e["name"] == "Fußarbeit")
    status, einheit = api.call("POST", "/api/trainings", {
        "dogId": dog_id, "date": str(today), "durationMinutes": 25, "notes": f"{PREFIX}Feedback",
        "exercises": [{"exerciseId": uebung["id"], "rating": 3, "difficulty": 0, "success": True, "notes": None}],
    }, owner_token)
    check(status in (200, 201), "Training angelegt", str(einheit))
    sid = einheit["id"]
    link = f"/dogs/{dog_id}?eintrag={sid}"

    check(api.call("PUT", f"/api/trainings/{sid}/feedback", {"feedback": "Selbstlob"}, owner_token)[0] == 400,
          "die Besitzerin kann sich kein Trainer-Feedback geben")
    check(api.call("PUT", f"/api/trainings/{sid}/feedback", {"feedback": "  "}, trainer_token)[0] == 400,
          "leeres Feedback wird abgelehnt")
    check(api.call("PUT", f"/api/trainings/{sid}/feedback", {"feedback": "Ruhiger abbiegen."}, trainer_token)[0] == 204,
          "Trainer:in gibt Feedback")
    meldungen = [n for n in benachrichtigungen(api, owner_token) if n["linkPath"] == link]
    check(len(meldungen) == 1, "Benachrichtigung führt auf den Eintrag (?eintrag=)", str(meldungen))
    _, gelesen = api.call("GET", f"/api/trainings/{sid}", token=owner_token)
    check(gelesen["trainerFeedback"] == "Ruhiger abbiegen." and gelesen["ownerReaction"] is None,
          "Feedback ohne Reaktion steht am Eintrag", str(gelesen["ownerReaction"]))

    # Antwort durch die Besitzerin, nicht durch die Trainer:in oder Fremde
    check(api.call("PUT", f"/api/trainings/{sid}/feedback-reply", {"reaction": 1, "reply": None}, trainer_token)[0] == 404,
          "Trainer:in kann nicht auf das eigene Feedback antworten (404)")
    check(api.call("PUT", f"/api/trainings/{sid}/feedback-reply", {"reaction": 1, "reply": None}, fremd_token)[0] == 404,
          "Fremde können nicht antworten (404)")
    check(api.call("PUT", f"/api/trainings/{sid}/feedback-reply", {"reaction": 7, "reply": None}, owner_token)[0] == 400,
          "unbekannte Reaktion -> 400")
    status, _ = api.call("PUT", f"/api/trainings/{sid}/feedback-reply",
                         {"reaction": 1, "reply": "Wie früh genau?"}, owner_token)
    check(status == 204, "Besitzerin antwortet mit 'Verstanden' und Rückfrage", str(status))
    _, gelesen = api.call("GET", f"/api/trainings/{sid}", token=owner_token)
    check(gelesen["ownerReaction"] == 1 and gelesen["ownerReply"] == "Wie früh genau?", "Reaktion und Rückfrage sind gespeichert")
    antworten = [n for n in benachrichtigungen(api, trainer_token) if n["linkPath"] == link]
    check(len(antworten) == 1, "Trainer:in wird benachrichtigt, Link auf denselben Eintrag", str(antworten))
    api.call("PUT", f"/api/trainings/{sid}/feedback-reply", {"reaction": 1, "reply": "Wie früh genau?"}, owner_token)
    check(len([n for n in benachrichtigungen(api, trainer_token) if n["linkPath"] == link]) == 1,
          "dieselbe Antwort noch einmal meldet nichts Neues")

    # Neues Feedback setzt die Antwort zurück
    api.call("PUT", f"/api/trainings/{sid}/feedback", {"feedback": "Etwa eine Schrittlänge vorher."}, trainer_token)
    _, gelesen = api.call("GET", f"/api/trainings/{sid}", token=owner_token)
    check(gelesen["ownerReaction"] is None and gelesen["ownerReply"] is None,
          "neues Feedback setzt Reaktion und Rückfrage zurück")
    api.call("DELETE", f"/api/trainings/{sid}", token=owner_token)


# ---------------------------------------------------------------- Anmeldungen


def _ablauf_anmeldungen(api, group_id, club_name, owner_token, trainer_token, today, frontend) -> None:
    section("Gruppen-Anmeldungen: Formular, Liste, Anwesenheit, Import")
    gruppe = f"/api/groups/{group_id}"
    name = f"{PREFIX}nf-gruppe"

    check(api.call("GET", f"{gruppe}/registration-link", token=trainer_token)[0] == 204, "zunächst gibt es keinen Formular-Link")
    check(api.call("POST", f"{gruppe}/registration-link", token=owner_token)[0] == 404, "Mitglied kann keinen Formular-Link erzeugen")
    status, link = api.call("POST", f"{gruppe}/registration-link", token=trainer_token)
    check(status == 200 and len(link["code"]) == 22, "Trainer:in erzeugt den Formular-Link", str(link))
    code = link["code"]
    form = f"/api/groups/registration/{code}"

    status, vorschau = api.call("GET", form)
    check(status == 200 and vorschau == {"clubName": club_name, "groupName": name},
          "Formularseite nennt Verein und Gruppe, sonst nichts", str(vorschau))
    check(api.call("GET", f"/api/groups/registration/{uuid.uuid4().hex[:22]}")[0] == 404, "falscher Code -> 404")
    if frontend:
        _robots_header(frontend, f"/anmeldung/{code}", "Anmeldeseite /anmeldung/")

    gueltig = {
        "firstName": "Egon", "lastName": "E2E-Test", "dogName": f"{PREFIX}Fido", "dogBreed": "Testhund",
        "dogBirthDate": str(today - timedelta(days=70)), "phone": "0711 555 9901", "consent": True, "website": None,
    }
    status, _ = anonym_schreiben(api, form, gueltig)
    check(status == 204, "anonyme Anmeldung wird angenommen", str(status))
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(len(liste) == 1 and liste[0]["source"] == 0 and liste[0]["dogName"] == f"{PREFIX}Fido",
          "Trainer:in sieht sie mit Quelle Formular", str(liste))
    meldungen = mit_text(benachrichtigungen(api, trainer_token), f"Neue Anmeldung für {name}")
    check(len(meldungen) == 1 and meldungen[0]["linkPath"] == f"/trainer/{group_id}",
          "Trainer:in wird benachrichtigt", str(meldungen))

    # Abwehr
    status, _ = anonym_schreiben(api, form, {**gueltig, "dogName": f"{PREFIX}Bot", "phone": "0711 555 9902", "website": "http://spam.example"})
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(status == 204 and len(liste) == 1, "Honeypot: Erfolgsantwort, aber nichts gespeichert", f"{status} {len(liste)}")
    status, body = anonym_schreiben(api, form, {**gueltig, "dogName": f"{PREFIX}Ohne", "phone": "0711 555 9903", "consent": False})
    check(status == 400 and "stimme zu" in json.dumps(body, ensure_ascii=False), "ohne Einwilligung -> 400", str(body))
    status, _ = anonym_schreiben(api, form, gueltig)
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(status == 204 and len(liste) == 1, "Duplikat: Erfolgsantwort, es bleibt bei einer Anmeldung", f"{status} {len(liste)}")
    status, _ = anonym_schreiben(api, form, {**gueltig, "dogName": f"{PREFIX}Zukunft", "phone": "0711 555 9904",
                                              "dogBirthDate": str(today + timedelta(days=3))})
    check(status == 400, "Wurftag in der Zukunft -> 400", str(status))
    status, _ = anonym_schreiben(api, form, {**gueltig, "dogName": f"{PREFIX}Tel", "phone": "abc"})
    check(status == 400, "ungültige Telefonnummer -> 400", str(status))
    check(api.call("GET", f"{gruppe}/registrations", token=owner_token)[0] == 404, "Mitglied sieht die Anmeldungen nicht (404)")

    # von Hand, Bezahlt, Anwesenheit
    status, hand = api.call("POST", f"{gruppe}/registrations", {
        "firstName": "Hanni", "lastName": "E2E-Hand", "dogName": f"{PREFIX}Luna", "dogBreed": "Testhund",
        "dogBirthDate": str(today - timedelta(days=100)), "phone": "0711 555 9910", "notes": "Ängstlich bei Männern",
    }, trainer_token)
    check(status == 200 and hand["source"] == 2 and hand["notes"] == "Ängstlich bei Männern" and hand["paidAt"] is None,
          "von Hand angelegt (Quelle Manuell, mit Notiz, unbezahlt)", str(hand))
    rid = hand["id"]

    status, bezahlt = api.call("PUT", f"{gruppe}/registrations/{rid}/paid", {"paid": True}, trainer_token)
    check(status == 200 and bezahlt["paidAt"] is not None, "als bezahlt markiert", str(bezahlt))
    status, offen = api.call("PUT", f"{gruppe}/registrations/{rid}/paid", {"paid": False}, trainer_token)
    check(status == 200 and offen["paidAt"] is None, "und wieder zurückgenommen", str(offen))

    gestern, uebermorgen = today - timedelta(days=1), today + timedelta(days=2)
    status, body = api.call("PUT", f"{gruppe}/registrations/{rid}/attendance", {"date": str(uebermorgen), "present": True}, trainer_token)
    check(status == 400, "Anwesenheit in der Zukunft wird abgelehnt", str(body))
    check(api.call("PUT", f"{gruppe}/registrations/{rid}/attendance", {"date": str(gestern), "present": True}, trainer_token)[0] == 204,
          "Anwesenheit für gestern eingetragen")
    check(api.call("PUT", f"{gruppe}/registrations/{rid}/attendance", {"date": str(gestern), "present": True}, trainer_token)[0] == 204,
          "nochmal dasselbe ist kein Fehler")
    _, tag = api.call("GET", f"{gruppe}/registrations/attendance?date={gestern}", token=trainer_token)
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    zeile = next(r for r in liste if r["id"] == rid)
    check(tag == [rid] and zeile["attendanceCount"] == 1, "der Tag zeigt sie, die Zählung steht bei 1", f"{tag} {zeile['attendanceCount']}")
    check(api.call("PUT", f"{gruppe}/registrations/{rid}/attendance", {"date": str(gestern), "present": False}, trainer_token)[0] == 204,
          "Anwesenheit zurückgenommen")
    check(api.call("GET", f"{gruppe}/registrations/days", token=trainer_token)[0] == 200, "Tage mit Termin abrufbar")

    # Import mit Duplikat- und Fehlerzeile
    status, ergebnis = api.call("POST", f"{gruppe}/registrations/import", {"rows": [
        {"zeile": 2, "firstName": "Ida", "lastName": "E2E-Import", "dogName": f"{PREFIX}Karl", "dogBreed": "Testhund",
         "dogBirthDate": str(today - timedelta(days=90)), "phone": "0711 555 9920", "registeredAt": None},
        {"zeile": 3, "firstName": "Egon", "lastName": "E2E-Test", "dogName": f"{PREFIX}Fido", "dogBreed": "Testhund",
         "dogBirthDate": str(today - timedelta(days=70)), "phone": "0711 555 9901", "registeredAt": None},
        {"zeile": 4, "firstName": "Jan", "lastName": "E2E-Fehler", "dogName": f"{PREFIX}Ohne", "dogBreed": "Testhund",
         "dogBirthDate": str(today - timedelta(days=90)), "phone": "", "registeredAt": None},
    ]}, trainer_token)
    check(status == 200 and ergebnis["angelegt"] == 1 and ergebnis["uebersprungen"] == 1
          and [f["zeile"] for f in ergebnis["fehler"]] == [4],
          "Import: eine angelegt, ein Duplikat übersprungen, Fehlerzeile 4 gemeldet", str(ergebnis))
    check(api.call("POST", f"{gruppe}/registrations/import", {"rows": []}, trainer_token)[0] == 400, "leerer Import -> 400")
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(sorted(r["source"] for r in liste) == [0, 1, 2], "die drei Quellen Formular, Import, Manuell sind vertreten",
          str([r["source"] for r in liste]))

    # Löschen einer Anmeldung
    check(api.call("DELETE", f"{gruppe}/registrations/{rid}", token=owner_token)[0] == 404, "Mitglied kann nichts löschen (404)")
    check(api.call("DELETE", f"{gruppe}/registrations/{rid}", token=trainer_token)[0] == 204, "Anmeldung gelöscht")
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(len(liste) == 2 and all(r["id"] != rid for r in liste), "und aus der Liste verschwunden")

    # Neuer Code entwertet den alten, Schließen sperrt das Formular
    status, neu = api.call("POST", f"{gruppe}/registration-link", token=trainer_token)
    check(status == 200 and neu["code"] != code, "neuer Code")
    check(api.call("GET", form)[0] == 404, "alter Code ist entwertet")
    status, _ = anonym_schreiben(api, form, {**gueltig, "dogName": f"{PREFIX}Alt", "phone": "0711 555 9930"})
    check(status == 404, "und nimmt nichts mehr an (404)", str(status))
    check(api.call("DELETE", f"{gruppe}/registration-link", token=owner_token)[0] == 404, "Mitglied kann das Formular nicht schließen")
    check(api.call("DELETE", f"{gruppe}/registration-link", token=trainer_token)[0] == 204, "Trainer:in schließt das Formular")
    check(api.call("GET", f"/api/groups/registration/{neu['code']}")[0] == 404, "geschlossenes Formular -> 404")
    status, _ = anonym_schreiben(api, f"/api/groups/registration/{neu['code']}", {**gueltig, "dogName": f"{PREFIX}Zu", "phone": "0711 555 9931"})
    check(status == 404, "und nimmt nichts an", str(status))
    _, liste = api.call("GET", f"{gruppe}/registrations", token=trainer_token)
    check(len(liste) == 2, "nach alledem sind es weiterhin nur die zwei erlaubten Anmeldungen", str(len(liste)))


# ---------------------------------------------------------------- Admin


def _ablauf_admin(api, admin_token, nicht_admin_token, stats_vorher) -> None:
    section("Admin: Kennzahlen und verwaiste Daten")
    status, stats = api.call("GET", "/api/admin/stats", token=admin_token)
    check(status == 200, "Kennzahlen abrufbar", str(status))
    letzte = stats["last30Days"]
    check(letzte["activeAccounts"] <= stats["userCount"], "aktive Konten <= alle Konten",
          f"{letzte['activeAccounts']} > {stats['userCount']}")
    check(letzte["newAccounts"] <= stats["userCount"], "neue Konten <= alle Konten")
    check(all(letzte[k] <= letzte["newAccounts"] for k in ("withDog", "inClub", "withGoal", "viaClubLink")),
          "Teilmengen der neuen Konten sind nicht größer als die neuen Konten", str(letzte))
    check(api.call("GET", "/api/admin/stats", token=nicht_admin_token)[0] == 403, "Kennzahlen: Nicht-Admin wird abgewiesen")

    # NUR LESEN. Der Endpunkt orphaned-data/purge löscht Daten endgültig und wird von diesem
    # Skript nie aufgerufen - auch nicht, um ihn zu "testen".
    status, waisen = api.call("GET", "/api/admin/orphaned-data", token=admin_token)
    felder = {"konten", "trainings", "hunde", "ziele", "benachrichtigungen", "sonstige"}
    check(status == 200 and set(waisen) == felder and all(isinstance(v, int) and v >= 0 for v in waisen.values()),
          "verwaiste Daten: Zählung abrufbar", str(waisen))
    check(api.call("GET", "/api/admin/orphaned-data", token=nicht_admin_token)[0] == 403, "verwaiste Daten: Nicht-Admin wird abgewiesen")
    check(api.call("GET", "/api/admin/orphaned-data")[0] == 401, "verwaiste Daten: ohne Anmeldung gesperrt")


# --- Einstieg --------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default="https://api-test.dogity.net")
    parser.add_argument("--admin-email", default=ADMIN[0])
    parser.add_argument("--admin-password", default=ADMIN[1])
    parser.add_argument("--frontend", default=None,
                        help="Adresse des Frontends (z.B. http://127.0.0.1:3000): prüft zusätzlich die "
                             "X-Robots-Tag-Kopfzeile von /v/ und /anmeldung/. Ohne Angabe entfällt die Prüfung.")
    parser.add_argument("--cleanup-only", action="store_true", help="nur Reste eines Abbruchs entfernen")
    args = parser.parse_args()

    host = urllib.parse.urlparse(args.api).hostname or ""
    if host not in ERLAUBTE_ZIELE:
        print(f"{RED}{args.api} ist kein zugelassenes Ziel.{OFF}")
        print("  Dieses Skript legt Nutzer, Hunde und Gruppen an und löscht sie wieder -")
        print("  das gehört nicht auf eine Umgebung mit echten Mitgliedern.")
        print(f"  Zugelassen: {', '.join(sorted(ERLAUBTE_ZIELE))}")
        return 2

    if args.frontend:
        frontend_host = urllib.parse.urlparse(args.frontend).hostname or ""
        if frontend_host not in ERLAUBTE_FRONTENDS:
            print(f"{RED}{args.frontend} ist kein zugelassenes Frontend.{OFF}")
            print(f"  Zugelassen: {', '.join(sorted(ERLAUBTE_FRONTENDS))}")
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
        run_neue_funktionen(api, admin_token, args.frontend)
    finally:
        section("Aufräumen")
        cleanup(api, admin_token)
        after = snapshot(api, admin_token)
        problems = diff_state(before, after)
        check(not problems, "Ausgangszustand wiederhergestellt", "; ".join(problems))
        # Den Nachher-Stand ausgeben: dann muss niemand mit einem zweiten
        # Skript nachsehen - und läuft dabei ins Anmelde-Limit.
        print(f"  {DIM}{len(after['users'])} Nutzer: {', '.join(after['users'])}{OFF}")
        for name, club in after["clubs"].items():
            print(f"  {DIM}{name}: Trainer {club['trainers'] or '-'}, Gruppen {club['groups'] or '-'}{OFF}")

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
