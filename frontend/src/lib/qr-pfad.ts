/**
 * Macht aus der Modulmatrix eines QR-Codes einen SVG-Pfad.
 *
 * Waagerechte Reihen dunkler Module werden zu EINEM Rechteck zusammengefasst
 * ("M x y h<breite> v1 h-<breite> z"): ein Code mit rund 40 x 40 Modulen
 * bräuchte sonst bis zu 800 Einzelquadrate. Ein Pfad statt vieler Elemente
 * hält auch den Aushang schlank, und zusammenhängende Flächen haben im Druck
 * keine feinen Nähte zwischen den Quadraten.
 *
 * Reines Zeichnen, kein Markup aus Text: Das Ergebnis besteht nur aus
 * Buchstaben und Zahlen, die hier selbst erzeugt werden.
 */
export function qrPfad(matrix: readonly (readonly boolean[])[], rand = 0): string {
  const teile: string[] = [];
  matrix.forEach((zeile, y) => {
    let x = 0;
    while (x < zeile.length) {
      if (!zeile[x]) {
        x++;
        continue;
      }
      const start = x;
      while (x < zeile.length && zeile[x]) x++;
      const breite = x - start;
      teile.push(`M${start + rand} ${y + rand}h${breite}v1h-${breite}z`);
    }
  });
  return teile.join("");
}
