---
name: deployment-workflow-user-deploys
description: Deployment-Workflow Dogity — der Nutzer startet Deployments selbst; Claude sagt nur wann und verifiziert danach
metadata: 
  node_type: memory
  type: feedback
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-07-24T06:04:50.475Z
---

**Der Nutzer führt Deployments (Test und Prod) ab jetzt selbst aus.** Claude soll die Deploy-Skripte **nicht mehr per SSH selbst starten**, sondern: Code committen/pushen, dann dem Nutzer klar sagen, dass jetzt deployt werden kann (welche Umgebung), und **nach** dem Deploy das Ergebnis live verifizieren.

**Why:** Der 1Password-SSH-Agent verklemmt sich beim automatischen `ssh dogity ...` regelmäßig; wiederholte Fehlversuche riskieren fail2ban-Sperren. Der Nutzer deployt schneller manuell. Ausdrücklich gewünscht: "Ich werde in Zukunft die Deployments machen, du sagst mir wann und prüfst es danach."

**How to apply:**
- **Commit und Push nach `master` gehören zusammen — nie nachfragen, nie lokal liegen lassen.** Am 2026-09-10 committet, aber nicht gepusht und stattdessen gefragt; Rückmeldung: "warum ist nicht gepusht? ab nach master damit ich testen kann". Der Nutzer kann ohne Push nicht deployen.
- Nach `git push`: kurz melden "bereit für Test-Deploy" (bzw. Prod) statt selbst `deploy-test.sh`/`promote-to-prod.sh` aufzurufen.
- Wenn der Nutzer "Deployment ist angestartet/durch" sagt: abwarten bis fertig, dann Test-Site laden und die konkrete Änderung bei ~375 px prüfen (siehe [[mobile-app-first-no-horizontal-scroll]]).
- Prod bleibt ohnehin bestätigungspflichtig; Verifikation läuft auf Test, Prod nur Read-only-Checks.
