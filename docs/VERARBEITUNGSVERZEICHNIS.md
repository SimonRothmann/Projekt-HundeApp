# Verzeichnis von Verarbeitungstätigkeiten (Art. 30 DSGVO)

Internes Dokument. Gehört **nicht** auf die Website, muss aber auf Verlangen
der Aufsichtsbehörde vorgelegt werden können.

Die Pflicht trifft auch kleine Verantwortliche: Die Ausnahme in Art. 30 Abs. 5
DSGVO gilt nur für Verarbeitungen, die *gelegentlich* erfolgen. Ein dauerhaft
betriebenes Konto- und Tagebuchsystem ist das Gegenteil davon.

**Stand:** 2026-10-27 · **Nächste Prüfung:** bei jeder Änderung an den
verarbeiteten Daten (neue Felder, neuer Dienst, neuer Empfänger)

---

## 1. Verantwortlicher

| | |
|---|---|
| Name | Simon Rothmann |
| Anschrift | Hauptstr. 20/2, 76307 Karlsbad |
| Kontakt | siehe `frontend/src/lib/rechtliches.ts` |
| Datenschutzbeauftragter | nicht bestellt (Voraussetzungen des § 38 BDSG liegen nicht vor) |
| Aufsichtsbehörde | LfDI Baden-Württemberg, Stuttgart |

---

## 2. Verarbeitungstätigkeiten

### 2.1 Nutzerkonto und Anmeldung

| | |
|---|---|
| Zweck | Zugang zum persönlichen Trainingstagebuch, Zuordnung der Einträge, Schutz vor fremdem Zugriff |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | Vorname, Nachname, E-Mail, Passwort-Hash, Rolle, Registrierungszeitpunkt, Avatar-URL, Refresh-Token-Hashes |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Nutzungsverhältnis), lit. f (Missbrauchsabwehr) |
| Empfänger | Hosting-Dienstleister (Auftragsverarbeiter) |
| Drittland | nein |
| Löschfrist | bis zur Kontolöschung; Refresh-Tokens spätestens nach 60 Tagen, bei Kontolöschung sofort und endgültig (kein bloßer Widerruf; Altbestand gelöschter Konten wird beim Start bereinigt) |
| Tabellen | `users`, `user_roles`, `refresh_tokens` |
| Speicherung im Browser | Wer ohne Konto die Oberflächensprache wählt (Deutsch/English), dessen Browser merkt sich die Wahl im lokalen Speicher unter `dogity.sprache`. Reine Komforteinstellung: bleibt auf dem Gerät, wird nicht an den Server gesendet und wird beim Abmelden nicht entfernt. Rechtsgrundlage wie bei den übrigen Komforteinträgen im Browser (Kartenart, Schriftgröße): § 25 Abs. 2 TDDDG (zur Bereitstellung des ausdrücklich gewünschten Dienstes erforderlich), keine Einwilligung nötig. Löschung: Löschen der Browserdaten. |

### 2.2 Hunde und Trainingstagebuch

| | |
|---|---|
| Zweck | Führen des Trainingstagebuchs, Auswertung des Verlaufs, Prüfungsvorbereitung |
| Betroffene | Registrierte Nutzer:innen (Hundedaten sind über den Halter personenbeziehbar) |
| Datenkategorien | Hundename, Rasse, Geburtstag, Geschlecht, Notizen, Foto; Trainingsdatum/-zeit/-dauer, Ortsname und Koordinaten, Wetter, Übungen mit Bewertung und Notizen, Trainer-Rückmeldungen samt Antwort der Besitzer:in (Danke/Verstanden, kurze Rückfrage mit Zeitpunkt); Ziele und Trainingspläne; Prüfungsergebnisse (Datum, Punkte, Notiz); Trainingspausen des Hundes (Grund: verletzt, krank oder läufig, Beginn, Ende oder „bis auf Weiteres“, optionale Notiz, wer die Pause eingetragen hat); Gesundheitseinträge des Hundes (Art: Impfung, Entwurmung, Parasitenschutz, Tierarzttermin, Sonstiges; Titel, Datum, bei Terminen die Uhrzeit, „gültig bis“ bzw. „nächste fällig“, optionale Notiz, wer den Eintrag angelegt hat) |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Empfänger | Mitbesitzer:innen und zugeordnete Trainer:innen (innerhalb der App); Hosting-Dienstleister. Trainingspausen: Mitbesitzer:innen sehen alles; zugeordnete (betreuende) Trainer:innen sehen nur, DASS und BIS WANN der Hund pausiert - Grund und Notiz bekommen sie vom Server nie geliefert (eigenes DTO, `DogPauseQueries.ToDto`); alle anderen sehen nichts. Die Pausenliste (`GET /api/dogs/{id}/pausen`) liefert Trainer:innen nur laufende und kommende Pausen. Der Trainingsplan, den auch Trainer:innen sehen, rechnet mit den Pausenzeiträumen, die in das Planfenster fallen (auch vergangene; nur Daten, ohne Grund und Notiz). Gesundheitseinträge: nur Besitzer:innen und Mitbesitzer:innen; betreuende Trainer:innen und alle anderen bekommen vom Server nichts (404 auf `/api/dogs/{id}/gesundheit`, am Hund kein Gesundheitsstand, kein Feld im Trainer-DTO). |
| Drittland | nein |
| Besonderheit | Die Gesundheitsangaben der Trainingspause betreffen das Tier, nicht eine Person (keine Daten nach Art. 9 DSGVO); sie bleiben wegen des Bezugs zum Halter trotzdem auf die Besitzer:innen beschränkt. Eine Pause lässt sich bearbeiten, vorzeitig beenden und löschen (Löschen blendet aus wie bei allen Einträgen). Nicht Teil von Benachrichtigungen, Mails oder Push. Gleiches gilt für die Gesundheitseinträge des Hundes (Impfungen, Entwurmung, Tierarzttermine): Angaben über das Tier, keine Daten nach Art. 9 DSGVO, trotzdem auf die Besitzer:innen beschränkt; kein Foto des Impfpasses, kein Dateispeicher. Die Erinnerungen an Fristen und Termine (2.5c2) nennen Hundename und Titel des Eintrags. |
| Löschfrist | bis zur Löschung des Eintrags bzw. des Kontos; die Pausen und Gesundheitseinträge eigener Hunde gehen bei der Kontolöschung endgültig mit (`AccountDataService.PurgeAsync`, auch weich gelöschte), die eines geteilten Hundes bleiben bei den übrigen Besitzer:innen. Der Datenexport enthält sie (Auskunft nach Art. 15/20). |
| Tabellen | `dogs`, `dog_owners`, `dog_images`, `dog_pauses`, `dog_health_records`, `training_sessions`, `training_exercises`, `goals`, `training_plans`, `training_plan_items`, `exercise_masteries` |

### 2.3 Fährtenaufzeichnung (Standortdaten)

| | |
|---|---|
| Zweck | Aufzeichnen der gelegten Fährte und des Ablaufs, Auswertung von Abweichung, Gegenständen und Stockungen |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | GPS-Punkte (Koordinaten, Zeitstempel, Genauigkeit), Markierungen, errechnete Auswertungen |
| Besonderheit | Bewegungsprofil mit Metergenauigkeit – die eingriffsintensivste Verarbeitung der App. Start nur durch den Nutzer, zusätzlich durch die Browser-Standortfreigabe geschützt. Keine Hintergrundaufzeichnung. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Kernfunktion, vom Nutzer ausgelöst) |
| Empfänger | wie 2.2; **keine** Übermittlung an Kartendienste |
| Drittland | nein |
| Löschfrist | bis zur Löschung der Fährte bzw. des Kontos |
| Tabellen | `gps_tracks`, `gps_points`, `gps_walk_runs`, `gps_walk_points`, `gps_walk_stops` |

### 2.4 Verein, Gruppen und Gruppentraining

| | |
|---|---|
| Zweck | Mitglieder- und Gruppenverwaltung, Planung von Gruppentrainings, Zuordnung Trainer:in ↔ Hund |
| Betroffene | Mitglieder und Personen mit einer Rolle im Verein (Training, Ausbildungswart:in, Mitgliederverwaltung, Vorstand, Verwaltung) |
| Datenkategorien | Mitgliedschaften und Beitrittsanfragen mit Status, Zeitpunkt und Herkunft (Vereinsliste, Einladungslink oder Einladung durch eine Trainer:in), bei einer offenen Einladung die vom Verein eingegebene E-Mail-Adresse, Rollen im Verein, Gruppenzugehörigkeit, Trainingstermine und zugewiesene Übungen, Zu- und Absagen der Mitglieder zu Terminen (Zusage ja/nein, Zeitpunkt der Antwort) |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Empfänger | Nach Rolle, jeweils nur im eigenen Verein: Trainer:innen (Training) ihre Gruppen mit Mitgliedern und Terminen; Ausbildungswart:in und Verwaltung alle Gruppen und Termine des Vereins; Mitgliederverwaltung und Verwaltung die Mitgliederliste (mit E-Mail-Adresse), Beitrittsanfragen und Einladungen; Vorstand und Verwaltung die Vereinsübersicht (Name, Gruppenzugehörigkeit, Rollen und Mitglied-seit-Datum der Mitglieder ohne E-Mail-Adresse, dazu Zahlen des Vereins). Die Liste der Rollen im Verein sehen Ausbildungswart:in und Verwaltung, die E-Mail-Adresse der Rolleninhaber:innen nur Mitgliederverwaltung, Verwaltung und der Betreiber. Die Zu- und Absagen zu Terminen sehen die Trainer:innen der Gruppe, Ausbildungswart:in und Verwaltung sowie die dem Termin zugeordneten Trainer:innen mit Namen, andere Mitglieder der Gruppe nur als Zahlen, der Vorstand nur als Summen je Monat |
| Drittland | nein |
| Besonderheit | Vereinsübersicht (`GET /api/clubs/{id}/overview` und `/overview/members`, nur Lesen): rechnet ausschließlich mit Vereinsdaten (Mitgliedschaften, Rollen, Gruppen, Gruppentermine, Summen der Zusagen je Monat) - keine Trainingstagebücher, keine App-Nutzungszeiten einzelner Personen, keine neue Datenart; die Mitgliederliste dort enthält keine E-Mail-Adresse, keine Hunde und keine Zusagen einzelner Personen. Der Vorstand ändert nichts (reine Leserolle) und ist keine Trainer:in. Zusagen: je Termin und Person eine Zeile (`group_training_session_responses`), nur aktive Gruppenmitglieder dürfen antworten und nur vor Beginn eines geplanten Termins; gezählt werden nur aktuelle Mitglieder. Eine Absage löst eine Benachrichtigung an die Trainer:innen des Termins aus, die Absage eines Termins eine an alle, die zugesagt hatten. „Zum Kalender“ erzeugt die Kalenderdatei auf dem Gerät (Gruppe, Zeit, Ort, Dauer – keine Personendaten), es wird nichts an den Server oder Dritte übertragen. Einladungslink/QR-Code je Verein (`clubs.InviteCode`, 128 Bit Zufall): Der Code selbst ist kein personenbezogenes Datum. Die öffentliche Einladungsseite gibt ohne Anmeldung nur den Vereinsnamen heraus; der Beitritt bleibt eine Anfrage, die der Verein freigibt. Der Code lässt sich jederzeit ersetzen oder abschalten. Einladung per E-Mail-Adresse: Niemand wird ohne eigene Zustimmung Mitglied. Lädt eine Trainer:in des Vereins eine Person ein, entsteht eine Mitgliedschaftszeile mit Status „Invited“ (`club_memberships.InvitedEmail`, die eingegebene Adresse); der Verein sieht bis zur Zusage nur diese Adresse und das Datum – keinen Namen –, die eingeladene Person wird benachrichtigt und sieht auf der Startseite und unter „Vereine“, wer einlädt. Eine Einladung zählt nirgends als Mitgliedschaft. Mitglied wird die Person erst mit „Annehmen“ (oder wenn sie selbst um Aufnahme bittet); Trainer:in kann nur werden, wer bereits freigegebenes Mitglied ist. |
| Löschfrist | bis zum Austritt bzw. zur Kontolöschung; die bei einer Einladung eingegebene Adresse wird mit der Antwort der Person (Annehmen oder Ablehnen) bzw. mit dem Zurückziehen durch den Verein gelöscht, eine unbeantwortete Einladung bleibt bis dahin bestehen (wie bei Gruppen- und Hundeeinladungen gibt es keine automatische Frist), spätestens mit der Kontolöschung |
| Tabellen | `clubs`, `club_memberships`, `club_trainers`, `club_registrations`, `groups`, `group_members`, `group_trainers`, `trainer_assignments`, `group_training_*` (u. a. `group_training_session_responses`) |

### 2.4a Anmeldung zu einer Gruppe über ein Anmeldeformular (ohne Konto)

| | |
|---|---|
| Zweck | Organisation einer Gruppe (z. B. Welpengruppe) durch den Verein: Wer ist angemeldet, wer ist erreichbar, wer war an welchem Tag da, wer hat bezahlt |
| Betroffene | Kursteilnehmende (Menschen **ohne** Dogity-Konto, die sich über Link/QR-Code anmelden oder von Trainer:innen eingetragen bzw. aus einem früheren Google-Formular übernommen werden) |
| Datenkategorien | Vorname, Nachname, Rufname und Rasse des Hundes, Wurftag, Telefonnummer; Zeitpunkt der Anmeldung und Herkunft (Formular, Import, von Hand); Anwesenheit je Tag (ggf. Verweis auf den Termin der Gruppe), Bezahlt-Zeitpunkt, optionale Notiz der Trainer:innen; Verweis auf die Trainer:in, die „bezahlt“ gesetzt bzw. abgehakt hat |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Umsetzung der Anmeldung zur Gruppe). Das Pflicht-Häkchen im Formular dient der Transparenz, nicht als Einwilligung nach lit. a. |
| Empfänger | Trainer:innen der Gruppe (Leitung, weitere Trainer:innen) sowie Ausbildungswart:in und Verwaltung des Vereins, zu dem die Gruppe gehört – sonst niemand; Hosting-Dienstleister (Auftragsverarbeiter). Das öffentliche Formular gibt ohne Anmeldung nur Vereins- und Gruppenname heraus. |
| Drittland | nein |
| Besonderheit | Zugang allein über den geheimen Code im Link (`groups.RegistrationCode`, 128 Bit Zufall, Erzeugung wie beim Vereins-Einladungslink; ersetzbar und abschaltbar). Das öffentliche Formular ist gegen Missbrauch geschützt: Ratenbegrenzung je IP (10 Anmeldungen/Minute), Köder-Feld gegen Roboter, serverseitige Prüfung aller Felder, Obergrenze von 500 Anmeldungen je Gruppe, Doppelanmeldungen (gleiche normalisierte Telefonnummer und gleicher Rufname innerhalb von 30 Tagen) legen nichts neu an. Die Seite hat `noindex` und `no-referrer`. Eine neue Anmeldung löst eine In-App-Benachrichtigung an die Trainer:innen der Gruppe aus (Rufname und Rasse des Hundes). CSV-Import: Die Datei wird im Browser gelesen, nur geprüfte Zeilen gehen an den Server. Die Anmeldungen sind **nicht** Teil des Datenexports einer Trainer:in (fremde Daten). |
| Löschfrist | Löschen durch die Trainer:innen jederzeit (endgültig, samt Anwesenheit); automatisch endgültig nach 12 Monaten ohne Aktivität (täglicher Hintergrunddienst `RegistrationRetentionBackgroundService`; Aktivität = Anlegen in Dogity, letzte Änderung inkl. „bezahlt“, letzter Anwesenheits-Haken – nicht der Google-Zeitstempel eines Imports); mit Löschen der Gruppe; Anmeldungen zu Gruppen, die es nicht mehr gibt, räumt `CommunityOrphanCleanup` beim Start weg. Bei der Löschung des Kontos einer Trainer:in bleiben die Anmeldungen (sie gehören dem Verein), nur die Verweise auf sie werden auf null gesetzt; wird die Gruppe dabei mangels Nachfolger:in geschlossen, gehen die Anmeldungen mit. Sicherungen: siehe Abschnitt 2.6. |
| Tabellen | `group_registrations`, `group_registration_attendances`, Spalte `groups.RegistrationCode` |

### 2.5 Sachkunde-Lernmodul und Benachrichtigungen

| | |
|---|---|
| Zweck | Lernfortschritt im Fragentrainer, Ergebnisse der Übungsprüfung (Verlauf „wie ist es zuletzt gelaufen?“), Hinweise innerhalb der App |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | je Frage: Lernfach, richtige/falsche Antworten, Zeitpunkte; je abgegebener Übungsprüfung (seit Fassung 0.39): Katalog, Zeitpunkt, erreichte und mögliche Punkte, bestanden ja/nein, Zahl der Fragen und der voll richtigen, Punkte je Themenkomplex, eine zufällige technische Abgabe-Kennung (seit Fassung 0.40, schließt die Doppelabgabe; ohne eigene Bedeutung). Die einzelnen Antworten werden **nicht** gespeichert; falsch beantwortete Fragen wirken wie eine falsche Antwort im Lernmodus (Fehlerspeicher in `quiz_masteries`). Benachrichtigungstexte mit Zeitpunkt und Lesestatus |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b |
| Besonderheit | Ohne Anmeldung ist die Übungsprüfung nutzbar, das Ergebnis wird dann nur angezeigt und nicht gespeichert (der Server hält keine Prüfung vor, die Abgabe wird bei Eingang gewertet). Die laufende Prüfung eines Browsers liegt nur im `sessionStorage` des Geräts und verlässt es nicht. „Lernstand zurücksetzen“ löscht die Prüfungsergebnisse **nicht**: Sie sind ein Verlauf, kein Lernstand; sie gehen mit der Kontolöschung. |
| Löschfrist | bis zur Kontolöschung (endgültig, auch weich Gelöschtes); Teil des Datenexports („Sachkunde“ und „Uebungspruefungen“) |
| Tabellen | `quiz_masteries`, `quiz_exam_attempts`, `notifications` |

### 2.5a Kennzahlen der Plattform (nur Admin)

| | |
|---|---|
| Zweck | Beurteilen, ob Einladungen und Erststart wirken (neue Konten der letzten 30 Tage; wie viele davon einen Hund, einen Verein, ein Prüfungsziel haben oder über einen Vereinslink kamen; wie viele Konten aktiv sind) und - seit der Nutzungsstatistik (`/admin/statistik`, Abschnitt „Nutzung“) - ob und wie die App verwendet wird und was bisher gar nicht verwendet wird |
| Betroffene | registrierte Nutzer:innen |
| Datenkategorien | ausschließlich Zählungen, die bei Abruf aus bestehenden Beständen berechnet werden (Anlegedatum des Kontos, Besitz eines Hundes, Mitgliedschaft und deren Herkunft, Ziele, angelegte Trainings/Fährten). Es wird nichts zusätzlich erhoben, nichts im Browser gemessen, keine Dritten eingebunden. Die Nutzungsstatistik zählt zusätzlich aus bestehenden Beständen: Anlegezeitpunkte der Einträge je Funktion (Training, eingetragene Übungen, Fährte legen/ablaufen, Ziel, Plan, Pause, Gesundheit, Sachkunde-Antworten und -Prüfungen, Hundefoto, Mitbesitz, Trainer-Betreuung und -Rückmeldung, Termine, Zu-/Absagen, Gruppenanmeldungen, Vereinsbeitritt, Push), die abgeschalteten Module, die Sportart-Auswahl und die Zeitpunkte der Anmelde-Tokens. Ausgegeben werden nur Summen (Konten je Funktion, Einträge je Woche, Wochentag und Stunde, Konten je abgeschaltetem Modul); keine Ids, Namen oder Adressen verlassen den Dienst. Es gibt keine neue Datenart, keine Seitenaufrufzählung und keine längere Aufbewahrung. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. f (Weiterentwicklung des Angebots); der Eingriff ist gering, weil nur Summen ausgegeben werden |
| Empfänger | Betreiber (Admin-Übersicht); keine Personenlisten |
| Drittland | nein |
| Definitionen (Nutzungsstatistik) | **Aktiv** = das Konto hat in den letzten 7/30/90 Tagen etwas angelegt (Zeitpunkt des Anlegens; Push einschalten und Anmeldungen auf Gruppenlisten zählen nicht). Je Funktion: Konten und Einträge der letzten 30 Tage, Konten insgesamt, Tag der letzten Nutzung. Wo eine Tabelle keine Person kennt, gilt die nächstliegende Zuordnung (Fährte und Ablauf: die Person des Trainings; Hundedaten ohne „angelegt von“: die Besitzer:innen des Hundes). Gelöschte Datensätze und Konten, die es nicht mehr gibt, zählen nicht. Wochen: Montag bis Sonntag, deutsche Zeit. **App geöffnet** (7/30 Tage) zählt Konten mit einem Anmelde-Token, der in dem Zeitraum entstand (bei Anmeldung und bei jeder Erneuerung der Sitzung, spätestens stündlich während der Nutzung). Die Zahl ist ein Richtwert und liegt eher zu niedrig: Öffnet jemand die App innerhalb der Gültigkeit des Zugangstokens (60 Minuten) erneut, entsteht kein neuer Token; reine Offline-Nutzung bleibt unsichtbar. Die Aufbewahrung der Tokens ändert sich dadurch nicht. |
| Löschfrist | keine eigene Speicherung - die Zahlen entstehen bei jedem Abruf neu |

### 2.5b E-Mail-Versand

| | |
|---|---|
| Zweck | Willkommensmail nach der Registrierung, Mail zum Zurücksetzen des Passworts, wichtige Benachrichtigungen (Einladungen in Verein, Gruppe, als Mit-Trainer:in einer Gruppe, zum Mitbesitz eines Hundes; Entscheidungen über Beitrittsanfragen an Verein oder Gruppe, angenommen/abgelehnt). Alles andere kommt nur als Benachrichtigung in der App bzw. als Push. Mails, Benachrichtigungen und Push werden in der Sprache der Empfänger:in geschrieben (Deutsch oder Englisch): Sie ist die Oberflächensprache, die bei der Registrierung gewählt war (gespeichert in `user_preferences.Locale`, bis die Person sie im Profil ändert); ohne gespeicherte Sprache gilt Deutsch. Schon verschickte Benachrichtigungen werden nicht nachträglich übersetzt. |
| Betroffene | Registrierte Nutzer:innen |
| Datenkategorien | E-Mail-Adresse (Empfänger), Vorname (Anrede), Sprache (nur zur Wahl der Textfassung), Inhalt der Mail: Text der Benachrichtigung (kann Namen von Vereinen, Gruppen, Hunden und der einladenden Person enthalten) bzw. der Link zum Zurücksetzen des Passworts. Absender- und Antwortadresse sind Adressen des Betreibers. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Willkommens- und Passwort-Mail gehören zum Konto; Einladungen und Entscheidungen sind Teil der Vereins-/Gruppenfunktion). Die zusätzliche Mail zu Einladungen und Entscheidungen ist je Person unter Profil → Funktionen abschaltbar (`user_preferences.EmailNotifications`, Vorgabe ein); Willkommens- und Passwort-Mail sind davon unabhängig. Die Wahl liegt schon bei der Registrierung („Wichtiges auch per E-Mail“, vorausgewählt – das ist keine Einwilligung nach Art. 6 Abs. 1 lit. a, sondern eine Einstellung zu vertragsbezogenen Mitteilungen). Jede Benachrichtigungs- und die Willkommensmail enthält einen Abmelde-Link, der ohne Anmeldung wirkt und denselben Schalter auf „aus“ setzt (die Seite bietet „Wieder anmelden“). Der Link enthält nur eine signierte Kontokennung (ASP.NET-DataProtection, eigener Verwendungszweck, kein Ablauf), keine Daten im Klartext; die Passwort- und die Testmail haben keinen Abmelde-Link. |
| Empfänger | keiner außer dem Mailanbieter der Empfänger:in: Versand über den eigenen Mailserver (Container `mail`, Postfix + OpenDKIM) auf dem Server bei Contabo, kein Versanddienst. Contabo als Hoster (Auftragsverarbeiter, Abschnitt 3). |
| Drittland | nein; wohin der Mailanbieter der Empfänger:in die Mail speichert, liegt bei ihr |
| Besonderheit | Der Versand läuft nicht im Request, sondern über eine In-Memory-Warteschlange mit Hintergrunddienst: ein Wiederholungsversuch, dann wird die Mail verworfen. Es wird nichts dauerhaft gespeichert; bei einem Neustart verfällt, was noch in der Warteschlange lag. Im Log erscheinen weder Empfänger noch Inhalt noch Links (Ausnahme: Development, Textfassung des Platzhalter-Senders ohne SMTP). Ohne `Email:Smtp:Host` wird nichts versendet. Der Mailserver nimmt Mails nur aus dem internen Netz und nur mit Absender @dogity.net an, signiert sie mit DKIM und überträgt sie per TLS, wenn der Empfänger es anbietet. Beim Zurücksetzen des Passworts gilt unverändert: Die Antwort ist für vorhandene und unbekannte Adressen gleich. Ist kein Mailversand eingerichtet, benachrichtigt die App stattdessen die Admins. |
| Löschfrist | Die Warteschlange des Mailservers (Volume `mail-spool`) hält eine Mail bis zur Annahme beim Empfänger, höchstens 2 Tage (`maximal_queue_lifetime`), danach Rückläufer an den Absender und Löschung. Das Postfix-Protokoll (stdout → Container-Log) enthält Empfängeradresse, Message-ID, Zeitpunkt und Zustellstatus, keinen Inhalt und keinen Betreff; es rolliert wie alle Container-Logs (10 MB × 3, Abschnitt 2.6). |
| Tabellen | keine; Einstellung in `user_preferences` |

### 2.5c Push-Benachrichtigungen (Web Push)

| | |
|---|---|
| Zweck | Benachrichtigungen der App zusätzlich auf dem Handy oder Rechner anzeigen (auf dem iPhone nur für Dogity als Home-Bildschirm-App), mit Zahl am App-Symbol |
| Betroffene | Registrierte Nutzer:innen, die Push auf einem Gerät selbst einschalten |
| Datenkategorien | Je Gerät: Adresse beim Push-Dienst des Geräteherstellers (Endpoint, zugleich Kennung des Geräts), Schlüssel des Geräts zur Verschlüsselung (p256dh, auth), Kurzbezeichnung des Geräts (z. B. „iPhone (Safari)“, aus dem User-Agent gebildet, der selbst nicht gespeichert wird), Zeitpunkte von Anmeldung und letzter Zustellung. Inhalt einer Push-Nachricht: der Text der Benachrichtigung, Zieladresse, Anzahl ungelesener Meldungen. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. a (Einwilligung): Push beginnt erst mit „Einschalten“ und der Erlaubnis im Browser bzw. Betriebssystem, je Gerät, jederzeit widerrufbar (Ausschalten in der App oder Entzug der Erlaubnis im Gerät); § 25 Abs. 1 TDDDG (Zugriff auf Endeinrichtung) ebenso |
| Empfänger | Push-Dienst des Geräteherstellers bzw. Browser-Anbieters (Apple, Google, Mozilla, bei Edge Microsoft) – siehe Abschnitt 4. Er erhält die Geräteadresse und die Nachricht **Ende-zu-Ende verschlüsselt** (RFC 8291) und kann sie nicht lesen. Hosting-Dienstleister (Auftragsverarbeiter) für die Speicherung der Abos. |
| Drittland | **USA möglich** (Apple, Google, Microsoft; Mozilla: weltweit) – die Nachricht ist verschlüsselt, Metadaten (Geräteadresse, Zeitpunkt, IP des Servers) sind dem Dienst bekannt |
| Besonderheit | Der Server nimmt als Push-Adresse nur Adressen bekannter Push-Dienste an (https, feste Hostnamen), damit er nicht zu Anfragen an interne Adressen missbraucht werden kann. Höchstens 20 Geräte je Person. Meldet sich auf demselben Gerät eine andere Person an, gehört das Abo danach ihr; beim Abmelden entfernt die App das Abo des Geräts. Meldet der Push-Dienst ein Gerät als unbekannt (HTTP 404/410), wird es gelöscht. Nachricht höchstens einen Tag gültig. Schlüssel des Geräts stehen weder im Datenexport noch im Log. |
| Löschfrist | bis zum Ausschalten, Abmelden oder zur Kontolöschung; unbekannte Geräte beim nächsten Versand; bei Kontolöschung endgültig (`AccountDataService.PurgeAsync`), nicht nur ausgeblendet |
| Tabellen | `push_subscriptions` |

### 2.5c2 Automatische Erinnerungen (Termin, Fährte, Gesundheit, Rückmeldungen)

| | |
|---|---|
| Zweck | Vier Erinnerungen, die der Server von sich aus als Meldung in der Glocke und – wo Push eingeschaltet ist (2.5c) – als Push schickt: (a) am Vortag ab 18 Uhr an einen Gruppentermin (Mitglieder der Gruppe; mit Zusage „du hast zugesagt“, ohne Antwort „kommst du?“, nach Absage nichts); (b) etwa drei Stunden nach dem Legen an eine Fährte ohne gespeicherten Ablauf, solange die 24-Stunden-Frist läuft und der Hund nicht in einer Trainingspause ist (an die Person, die das Training eingetragen hat, sofern sie den Hund noch sehen darf); (c) an Trainer:innen mit betreuten Hunden eine Zusammenfassung, wie viele Trainings auf eine Rückmeldung warten (nur wenn mindestens eins wartet; Rhythmus wählen sie selbst); (d) an die Besitzer:innen und Mitbesitzer:innen eines Hundes: in den letzten 14 Tagen vor Ablauf einer eingetragenen Frist (Impfung gültig bis, Entwurmung nächste fällig) und am Tag selbst je einmal, bei einem Tierarzttermin am Vortag ab 18 Uhr; es erinnert nur der jüngste Eintrag je Art und Titel, Trainer:innen bekommen nichts. Nie per E-Mail. Versand nur zwischen 8 und 22 Uhr deutscher Zeit. Der Text entsteht in der Sprache der Empfänger:in. |
| Betroffene | Registrierte Nutzer:innen (Gruppenmitglieder, Hundehalter:innen, Trainer:innen) |
| Datenkategorien | Einstellungen in `user_preferences`: vier Schalter (Termine, Fährten, Gesundheit, Rückmeldungen), bei Trainer:innen Wochentage (Bitmaske) und Uhrzeit (volle Stunde). Merkzettel `sent_reminders`: Person, Art, Id des Termins bzw. der Fährte bzw. des Gesundheitseintrags (bei der Zusammenfassung leer) und bei der Zusammenfassung der Tag, bei Gesundheit der Tag der Frist bzw. des Termins, dazu der Zeitpunkt – kein Text. Der Text selbst steht als gewöhnliche Benachrichtigung in der Glocke (2.5) und enthält Gruppenname, Hundename, Titel des Eintrags, Uhrzeit bzw. Frist oder die Anzahl. |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. b (Teil der Funktionen Gruppentermine, Fährte, Gesundheit, Trainerübersicht); jede Art ist einzeln unter Profil → Funktionen abschaltbar, Vorgabe an. Als Push zusätzlich die Einwilligung nach 2.5c. |
| Empfänger | wie Benachrichtigungen und Push (2.5, 2.5c); keine zusätzlichen Empfänger |
| Drittland | wie 2.5c, nur wenn Push eingeschaltet ist |
| Besonderheit | Hintergrunddienst `ErinnerungBackgroundService` prüft alle 10 Minuten; was fällig ist, bestimmen reine Regeln (`Erinnerungsregeln`), die Abfragen holen nur Termine des Folgetags, Fährten der letzten 30 Stunden, Fristen der nächsten 14 Tage (und Tierarzttermine des Folgetags) und Trainer:innen mit Betreuungen. Das Modul „Gesundheit“ abgeschaltet = keine Gesundheitserinnerung. Der eindeutige Index auf `sent_reminders` (Person, Art, Bezug, Tag) verhindert Doppelversand bei Neustart und mehreren Instanzen: Der Eintrag wird vor dem Versand gespeichert („höchstens einmal“). Die Zusammenfassung zählt wie die Zahl auf der Trainer-Übersicht. Die Gesundheitsangabe der Trainingspause fließt nur als „pausiert: ja/nein“ in die Entscheidung, nie in einen Text. |
| Löschfrist | Merkzettel nach 60 Tagen (Aufräumen bei jedem Lauf, endgültig gelöscht); bei Kontolöschung sofort und endgültig (`AccountDataService.PurgeAsync`, auch weich gelöschte). Die Einstellungen gehen mit der Zeile in `user_preferences`. Der Datenexport enthält Einstellungen und Merkzettel (Auskunft nach Art. 15/20). |
| Tabellen | `sent_reminders`; Einstellungen in `user_preferences` (`ReminderSessions`, `ReminderTracks`, `ReminderHealth`, `ReminderTrainerSummary`, `TrainerSummaryDays`, `TrainerSummaryHour`) |

### 2.5d Kontakt per E-Mail (info@dogity.net)

| | |
|---|---|
| Zweck | Anfragen, Fehlermeldungen und Wünsche an den Betreiber beantworten; Antworten auf Mails der App (Reply-To) |
| Betroffene | alle, die an info@dogity.net schreiben |
| Datenkategorien | Absenderadresse, Name (soweit angegeben), Inhalt der Nachricht, Kopfzeilen |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. f (Anfragen beantworten); bei Fragen zum eigenen Konto lit. b |
| Empfänger | Cloudflare (Email Routing, nimmt die Mail für dogity.net an und leitet sie weiter, ohne sie zu speichern); E-Mail-Anbieter des persönlichen Postfachs des Betreibers |
| Drittland | **USA möglich** (Cloudflare, unter dem EU-U.S. Data Privacy Framework zertifiziert, Art. 45) |
| Löschfrist | nach Erledigung, sofern keine Aufbewahrungspflicht besteht |
| Tabellen | keine - nichts davon landet in der Datenbank |

### 2.6 Betrieb der Server

| | |
|---|---|
| Zweck | Auslieferung der Seite, Betrieb, Fehlersuche, Abwehr von Angriffen |
| Betroffene | alle Besucher:innen |
| Datenkategorien | IP-Adresse, Zeitpunkt, aufgerufene Adresse, Browserkennung; kurzzeitige Zählung der Anmeldeversuche und der Aufrufe von Vereins-Einladungslinks je IP |
| Empfänger | Cloudflare als vorgeschalteter Proxy (alle Anfragen, TLS endet dort), Contabo als Hoster |
| Rechtsgrundlage | Art. 6 Abs. 1 lit. f |
| Besonderheit | keine Zusammenführung mit Kontodaten, keine Auswertung des Nutzungsverhaltens |
| Löschfrist | Container-Logs rollieren (10 MB × 3 je Dienst); Sicherungen gestaffelt 14/14/70/400 Tage (siehe Abschnitt 5) |

---

## 3. Auftragsverarbeiter (Art. 28)

| Dienstleister | Leistung | Vertrag | Drittland |
|---|---|---|---|
| Contabo GmbH, München | Server, Datenbank, laufender Betrieb | AV-Vertrag erforderlich | nein (Standort Deutschland) |
| Cloudflare, Inc. | Auslagerung der verschlüsselten Datenbanksicherungen (R2) | AV-Vertrag erforderlich (Cloudflare DPA) | Bucket fest auf EU-Jurisdiktion gestellt; Endpunkt `.eu.r2.cloudflarestorage.com` |
| Cloudflare, Inc. | Vorgeschalteter Proxy für alle vier Domains: TLS, Abwehr von Angriffen und Bots, Weiterleitung an den Server. Fügt nach Dashboard-Einstellung ein Bot-Prüfskript, E-Mail-Verschleierung und Network Error Logging hinzu | derselbe AV-Vertrag (Cloudflare DPA) | **USA möglich** – Anfragen laufen meist über ein nahes Rechenzentrum; Cloudflare ist unter dem EU-U.S. Data Privacy Framework zertifiziert (Art. 45) |

Zum dritten Eintrag, am 2026-09-21 nachgetragen: Die DNS-Namen zeigen auf
Cloudflare, nicht auf den Server (Antworten tragen `cf-ray`). Damit läuft jede
Anfrage samt Passwort beim Anmelden durch dieses Netz. Die erste Fassung der
Datenschutzerklärung kannte Cloudflare nur als Sicherungsspeicher.

Zur Einordnung des Sicherungs-Eintrags: Die Sicherungen sind bereits auf dem
Server asymmetrisch verschlüsselt, bevor sie übertragen werden. Der private
Schlüssel liegt ausschließlich beim Verantwortlichen, nicht auf dem Server und
nicht beim Speicheranbieter - Cloudflare verwahrt damit einen Datenbestand,
den es nicht lesen kann. Das ersetzt den AV-Vertrag nicht, senkt aber das
Risiko erheblich.

**Offen:** Abschluss bzw. Nachweis der AV-Verträge (Contabo, Cloudflare)
dokumentieren.

Zum Mailversand, entschieden 2026-10-15: kein Versanddienst (Brevo verworfen),
sondern ein eigener Mailserver auf dem Contabo-Server - damit kein weiterer
Auftragsverarbeiter. Eingehende Mails an info@dogity.net laufen über
Cloudflare Email Routing (vom Cloudflare-DPA erfasst) in das persönliche
Postfach des Betreibers; siehe Abschnitt 2.5d. Der Anbieter dieses Postfachs
wird bewusst nicht öffentlich genannt (Entscheidung des Betreibers
2026-10-15); die Datenschutzerklärung nennt die Kategorie (Art. 13 Abs. 1 lit. e).

---

## 4. Eigenständig Verantwortliche (keine Auftragsverarbeitung)

| Stelle | Wann | Was fließt | Drittland |
|---|---|---|---|
| OpenStreetMap Foundation | Vektorkacheln der Karte (Standardansicht); das Gerät zeichnet die Karte selbst, Schriften und Symbole liefert Dogity | IP-Adresse und Kartenausschnitt, direkt vom Gerät | UK (Angemessenheitsbeschluss) |
| Esri | Luftbild-Kacheln, nur bei aktiv gewähltem Luftbild | IP-Adresse und Kartenausschnitt, direkt vom Gerät | **USA** |
| komoot GmbH (Photon) | Ortssuche | Suchbegriff, ggf. Umkreis-Koordinaten – **vom Server**, ohne Nutzer-IP | nein |
| Open-Meteo | Wetter zu Ort und Zeitpunkt | Koordinaten und Datum – **vom Server**, ohne Nutzer-IP | nein |
| Push-Dienste der Geräte-/Browserhersteller: Apple (`*.push.apple.com`), Google (`fcm.googleapis.com`), Mozilla (`*.push.services.mozilla.com`), Microsoft (`*.notify.windows.com`) | Zustellung von Push-Nachrichten an Geräte, die Push eingeschaltet haben (Abschnitt 2.5c); der Dienst gehört zu dem Gerät/Browser, das die Person nutzt, und wird von diesem ausgewählt – Dogity schickt an die Adresse, die das Gerät nennt | Geräteadresse (Endpoint), Zeitpunkt und Größe der Nachricht; der Inhalt ist Ende-zu-Ende verschlüsselt und für den Dienst nicht lesbar – **vom Server** | Apple, Google, Microsoft: **USA möglich**; Mozilla: weltweit |
| Ko-fi | nur ausgehender Verweis | nichts, solange der Link nicht angeklickt wird | USA (erst nach Klick) |

**Offen:** Für Esri prüfen, ob eine Teilnahme am EU-U.S. Data Privacy Framework
besteht. Falls nicht, Luftbild-Anbieter wechseln oder das Umschalten an eine
Einwilligung koppeln. Bis dahin mindert die Voreinstellung das Risiko: Die
Karte startet immer mit OpenStreetMap, das Luftbild lädt erst auf aktive
Auswahl.

---

## 5. Technische und organisatorische Maßnahmen (Art. 32)

- TLS für alle Verbindungen, HSTS, automatische Zertifikate über Let's Encrypt
- Passwörter nur als Hash (ASP.NET Core Identity)
- Kurzlebige Access-Tokens (60 Minuten), rotierende Refresh-Tokens mit
  Wiederverwendungserkennung, serverseitig widerrufbar
- Content-Security-Policy durchsetzend, `frame-ancestors 'none'`,
  `X-Content-Type-Options`, Referrer-Policy
- Rate-Limit auf den Anmeldewegen (10 Anfragen/Minute je IP; Token-Erneuerung
  mit eigenem, weiterem Topf). Die IP ist die echte Client-Adresse: Caddy
  vertraut nur den Adressbereichen von Cloudflare und reicht CF-Connecting-IP
  weiter
- Sperre nach fünf falschen Passwörtern - auch bei den Passwortabfragen im
  Profil (E-Mail ändern, Passwort ändern, Konto löschen)
- Passwort ändern oder zurücksetzen beendet alle anderen Sitzungen
- Zugriffsprüfung auf Datenebene: jeder Abruf ist an Besitz oder eine
  ausdrückliche Trainerzuordnung gebunden
- Eine Trainerzuordnung setzt eine Gruppenmitgliedschaft voraus, der die
  betroffene Person selbst zugestimmt hat (eigene Anfrage oder angenommene
  Einladung); sie wird benachrichtigt und beendet die Zuordnung durch Verlassen
  der Gruppe
- Getrennte Datenbanken für Test und Produktion, eigene Rollen; die Datenbank
  ist nicht nach außen veröffentlicht (nur im internen Docker-Netz erreichbar)
- Tägliche Sicherung der Produktionsdatenbank, asymmetrisch verschlüsselt
  (öffentlicher Schlüssel auf dem Server, privater Schlüssel nur beim
  Verantwortlichen), ausgelagert in einen EU-Speicher
- Aufbewahrung der Sicherungen gestaffelt: 14 Tage lokal, ausgelagert 14 Tage
  (täglich), 70 Tage (wöchentlich), 400 Tage (monatlich); gelöscht wird über
  Lifecycle-Regeln des Speichers, die auch dann greifen, wenn das
  Sicherungsskript ausfällt

---

## 6. Betroffenenrechte – technische Umsetzung

| Recht | Umsetzung |
|---|---|
| Auskunft und Übertragbarkeit (Art. 15, 20) | `GET /api/profile/export`, in der App unter Profil → „Daten & Konto“ → „Meine Daten herunterladen“; enthält auch die Geräte mit Push (Bezeichnung und Zeitpunkte, ohne Adresse und Schlüssel), die Einstellung zu E-Mails, die Einstellungen der Erinnerungen und die versendeten Erinnerungen (Art, Bezug, Zeitpunkt) |
| Löschung (Art. 17) | `DELETE /api/profile` mit Passwortbestätigung, in der App unter Profil → „Daten & Konto“ → „Konto löschen“; entfernt die Fachdaten und anschließend das Konto |
| Berichtigung (Art. 16) | Profil, Hunde- und Trainingsbearbeitung in der App |
| Angemeldete ohne Konto (2.4a) | Auskunft, Berichtigung, Löschung formlos beim Verein oder per E-Mail an den Betreiber; die Trainer:innen bearbeiten und löschen Anmeldungen in der App (Trainer-Seite der Gruppe). Es gibt keinen Selbstbedienungs-Zugang, weil es kein Konto gibt. |
| Einschränkung, Widerspruch (Art. 18, 21) | formlos per E-Mail |

---

## 7. Datenschutz-Folgenabschätzung (Art. 35)

Nicht erforderlich. Es werden keine besonderen Kategorien nach Art. 9
verarbeitet, es findet keine systematische Bewertung von Personen und keine
umfangreiche Beobachtung öffentlich zugänglicher Bereiche statt. Die
Standortdaten entstehen ausschließlich durch eine vom Nutzer selbst gestartete,
zeitlich begrenzte Aufzeichnung.

Diese Einschätzung ist neu zu treffen, falls eine Hintergrundaufzeichnung,
eine automatische Standorterfassung oder eine Auswertung über mehrere Personen
hinweg dazukommt.
