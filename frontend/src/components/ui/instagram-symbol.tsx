/**
 * Instagram-Symbol als eigenes Inline-SVG: lucide-react enthält kein
 * Markenzeichen mehr, und ein Bild von Instagram-Servern kommt nicht in Frage
 * (es ist nur ein Link, es fließen keine Daten an Meta).
 */
export const INSTAGRAM_URL = "https://www.instagram.com/dogityapp/";

export function InstagramSymbol({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden
    >
      <rect x="3" y="3" width="18" height="18" rx="5" />
      <circle cx="12" cy="12" r="4" />
      <circle cx="17.5" cy="6.5" r="0.6" fill="currentColor" />
    </svg>
  );
}
