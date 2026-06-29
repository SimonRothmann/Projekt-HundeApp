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

                        |

                  PWA Frontend

                   Next.js

                        |

                        |

                  API Gateway

                        |

               ASP.NET Core API

                        |

    -----------------------------------

    |                |                |

 PostgreSQL       Storage          Cache

 Database         Files            Redis

    |

    |

Background Worker

    |

    |

Prüfungsordnung Sync

Benachrichtigungen

Training Analyse


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

Exam

Sport

Group

User

Trainer


Keine Datenbankabhängigkeiten.

---

# Application Layer

Enthält:

- Use Cases
- Services
- Commands
- Queries

Beispiel:


CreateTrainingSession

GenerateTrainingPlan

AssignTrainer

SyncExamRegulation


---

# Infrastructure Layer

Enthält:

- Datenbank
- Files
- externe APIs
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

Für:

- Bilder
- Videos
- Dokumente
- Prüfungsunterlagen


Empfehlung:

S3 kompatibler Storage.

Beispiele:

- Cloudflare R2
- Hetzner Object Storage

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

Worker übernimmt:

- Prüfungsordnung prüfen
- Erinnerungen
- Trainingsanalyse
- Benachrichtigungen

---

# Sicherheit

Pflicht:

- HTTPS
- JWT / OAuth2
- Passwort Hashing
- Rollenprüfung
- Rate Limiting
- Audit Logs

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