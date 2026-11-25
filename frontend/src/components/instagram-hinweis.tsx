import { InstagramSymbol, INSTAGRAM_URL } from "@/components/ui/instagram-symbol";
import { useT } from "@/lib/i18n";

/** Dauerhafter, leiser Hinweis unter der Liste der Neuerungen. */
export function InstagramHinweis() {
  const t = useT();
  return (
    <a
      href={INSTAGRAM_URL}
      target="_blank"
      rel="noopener noreferrer"
      className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
    >
      <InstagramSymbol className="size-4 shrink-0" />
      <span className="min-w-0 [overflow-wrap:anywhere]">{t("Neuigkeiten auch auf Instagram")}</span>
    </a>
  );
}
