import { WifiOff } from "lucide-react";

export default function OfflinePage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 p-6 text-center">
      <WifiOff className="size-10 text-muted-foreground" />
      <h1 className="text-xl font-semibold">Keine Verbindung</h1>
      <p className="text-muted-foreground">
        Diese Seite wurde noch nicht offline gespeichert. Bereits besuchte Seiten und offline erfasste
        Trainings/Fährten funktionieren weiterhin.
      </p>
    </div>
  );
}
