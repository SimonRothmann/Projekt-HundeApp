import Link from "next/link";
import { ArrowLeft } from "lucide-react";

/**
 * Rückweg von den Anmeldeseiten zur Startseite.
 *
 * Ohne diesen Link sind Login, Registrierung und die Passwortseiten
 * Sackgassen: Wer über einen Suchtreffer direkt dort landet, hat keinen
 * Browserverlauf zum Zurückgehen und kommt gar nicht mehr zu den Inhalten,
 * die erklären, worum es überhaupt geht.
 *
 * Bewusst ein eigenes Bauteil und keine Kopie je Seite - vier Anmeldeseiten
 * driften sonst auseinander (siehe SubpageBackButton für den eingeloggten
 * Bereich).
 */
export function AuthBackLink() {
  return (
    <Link
      href="/"
      className="inline-flex items-center gap-1.5 self-start text-sm text-muted-foreground transition-colors hover:text-foreground coarse:min-h-11"
    >
      <ArrowLeft className="size-4" />
      Zurück zur Startseite
    </Link>
  );
}
