# Software Architecture Document

Projekt:
Dogity

Version:
1.0

---

# Architekturziel

Dogity soll:

- mit minimalen Kosten starten
- einfach deploybar sein
- offlinefähig funktionieren
- langfristig auf mehrere hunderttausend Nutzer skalieren können

Die Architektur folgt daher dem Prinzip:

"Simple first, scalable later."

Keine unnötige Komplexität am Anfang.

---

# Architekturprinzip

Dogity wird als modularer Monolith gestartet.

Warum kein Microservice-System?

Weil Microservices am Anfang:

- höhere Kosten verursachen
- mehr Infrastruktur benötigen
- Entwicklung verlangsamen

Die Anwendung wird aber so strukturiert,
dass einzelne Module später ausgelagert werden können.

---

# High Level Architektur

                     Benutzer

                        |

                  PWA Frontend

                   Next.js

                        |

                 Caddy (Reverse Proxy, TLS)

                        |

               ASP.NET Core API

        mit Hintergrunddiensten
        (BackgroundService, selber Prozess)

                        |

    -----------------------------------

    |                |                |

 PostgreSQL     Mailserver     Externe Dienste
 Database       (ausgehend)    Open-Meteo (Wetter)
                               Photon (Ortssuche)
                               Web Push

Es gibt keinen API-Gateway, keinen Cache-Dienst und keinen Dateispeicher.
Die Hintergrundarbeit besteht aus Mail-, Push-, Erinnerungs-, Wetter-,
Plan- und Aufbewahrungsdienst (siehe "Hintergrundprozesse").


---

# Frontend Architektur

Technologie:

Next.js

mit:

- React
- TypeScript
- Tailwind CSS
- Shadcn UI

---

Aufgaben:

- Benutzeroberfläche
- Offline Anwendung
- Routing
- State Management
- Synchronisation

---

# Backend Architektur

Technologie:

ASP.NET Core Web API

Warum?

- stabil
- performant
- Open Source
- langfristig wartbar

---

Struktur:


Backend

├── Api

├── Application

├── Domain

├── Infrastructure

├── Tests


---

# Domain Layer

Enthält Geschäftslogik.

Beispiele:


Dog

Training

Exercise

Goal

Sport

Regulation

Group

Club


Keine Datenbankabhängigkeiten.

---

# Application Layer

Enthält:

- Use Cases
- Services
- Commands
- Queries

Beispiel:


TrainingService (Training erfassen)

AdaptivePlanGenerator (Trainingsplan erzeugen)

GoalService (Ziele und Pläne)

ClubService (Vereine, Rollen, Mitglieder)


---

# Infrastructure Layer

Enthält:

- Datenbank (EF Core, Migrationen)
- externe APIs (Wetter, Ortssuche)
- E-Mail
- Push Notifications

---

# Datenbank

PostgreSQL.

Warum?

- Open Source
- günstig
- skalierbar
- JSON Unterstützung
- geografische Erweiterungen möglich

---

# Storage

Es gibt keinen Dateispeicher. Das einzige Bild, das Dogity speichert, ist das
Hundebild: als Bytes in `dog_images` (siehe DATABASE.md). Cloudflare R2 dient
nur den Datenbanksicherungen (docs/BACKUP.md).

Sollten später Videos oder Dokumente dazukommen: S3-kompatibler Storage
(z. B. Cloudflare R2) statt der Datenbank.

---

# Offline Architektur

Dogity wird als Progressive Web App umgesetzt.

---

Technik:

Service Worker

+

IndexedDB

+

Synchronisationsengine


---

Beispiel:

Training auf Hundeplatz:


User erstellt Training

    |

    |

Speicherung lokal

IndexedDB

    |

    |

Internet verfügbar

    |

    |

Synchronisation API

    |

    |

PostgreSQL


---

# Konfliktlösung

Wenn zwei Geräte dieselben Daten ändern:

Regeln:

1. letzte Änderung gewinnt

oder

2. Benutzerentscheidung

Heute wird offline nur Neues angelegt (Training, GPS-Aufzeichnung) und per
POST nachgeschickt; bearbeitet wird nur mit Verbindung. Zwei Geräte können
sich so nicht überschreiben.

---

# GPS Architektur

Fährten werden lokal aufgezeichnet.

Speicherung:


Track

|

GPS Punkte

|

Latitude

Longitude

Timestamp


---

Nach Synchronisierung:

PostGIS Erweiterung möglich.

---

# Hintergrundprozesse

Sechs `BackgroundService` im Backend-Prozess (`Api/Hosting`):

- Mailversand (Warteschlange)
- Push-Versand (Warteschlange)
- Erinnerungen (Termine, Fährten, Gesundheit, offene Rückmeldungen)
- Wetter-Nachtrag (Wetter zu Trainings und Fährten nachladen)
- Plan-Neuberechnung (adaptiver Trainingsplan)
- Aufbewahrung (Gruppen-Anmeldungen nach zwölf Monaten ohne Aktivität löschen)

---

# Sicherheit

Pflicht:

- HTTPS (Caddy)
- JWT mit erneuerbaren Anmeldungen (`refresh_tokens`)
- Passwort Hashing (ASP.NET Identity)
- Rollen- und Zuordnungsprüfung im Service (docs/BERECHTIGUNGEN.md)
- Rate Limiting an den anonymen Endpunkten

---

# Erweiterbarkeit

Neue Sportart:

Nicht:

Code schreiben.

Sondern:

Neue Daten:


Sportart

Prüfung

Übungen

Bewertung


---

Beispiel:

Neue Sparte:

Agility

benötigt:

Sport Tabelle

Exercise Tabelle

Regulation Tabelle


Kein Frontend Umbau.