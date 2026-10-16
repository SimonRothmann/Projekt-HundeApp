/**
 * Gemeinsame Bausteine der deutschen und der englischen Datenschutzerklärung.
 *
 * Beide Fassungen sehen gleich aus und sollen es bleiben; eine Kopie in der
 * englischen Datei liefe beim nächsten Umbau auseinander.
 */
export function Abschnitt({ titel, children }: { titel: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-border/60 py-8">
      <h2 className="text-xl font-bold tracking-tight sm:text-2xl">{titel}</h2>
      <div className="mt-4 flex flex-col gap-3 text-sm leading-relaxed text-muted-foreground">{children}</div>
    </section>
  );
}

export function Liste({ punkte }: { punkte: string[] }) {
  return (
    <ul className="flex list-disc flex-col gap-1.5 pl-5">
      {punkte.map((punkt) => (
        <li key={punkt}>{punkt}</li>
      ))}
    </ul>
  );
}
