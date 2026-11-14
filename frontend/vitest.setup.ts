import { ladeEnglisch } from "@/lib/i18n";

// Die App lädt die englische Fassung erst nach, wenn Englisch gilt (siehe
// ladeEnglisch). Die Tests prüfen Übersetzungen direkt und ohne Provider -
// für sie liegt das Wörterbuch deshalb von Anfang an bereit.
await ladeEnglisch();
