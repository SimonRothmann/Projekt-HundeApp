/**
 * Bild aus einer Datei-Auswahl auf Profilbildgröße rechnen.
 *
 * Passiert bewusst auf dem Gerät und nicht auf dem Server: Handykameras
 * liefern 3-6 MB, gebraucht werden davon wenige Zehn Kilobyte. Wer das
 * ungerechnet hochlädt, wartet auf dem Hundeplatz mit schlechtem Netz
 * minutenlang - und die Datenbank trüge die volle Größe mit.
 */

/** Kantenlänge, auf die das Bild eingepasst wird (quadratischer Zuschnitt). */
const TARGET_SIZE = 512;

/** JPEG-Qualität - darunter wird die Fellzeichnung sichtbar matschig. */
const QUALITY = 0.85;

export async function fileToSquareDataUrl(file: File): Promise<string> {
  const bitmap = await loadBitmap(file);

  // Mittigen quadratischen Ausschnitt nehmen: ein Profilbild wird rund
  // angezeigt, ein unbeschnittenes Hochformat würde dabei ohnehin oben und
  // unten abgeschnitten - nur eben unkontrolliert.
  const edge = Math.min(bitmap.width, bitmap.height);
  const sx = (bitmap.width - edge) / 2;
  const sy = (bitmap.height - edge) / 2;
  const size = Math.min(TARGET_SIZE, edge);

  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("Bild konnte nicht verarbeitet werden.");

  ctx.drawImage(bitmap, sx, sy, edge, edge, 0, 0, size, size);
  if ("close" in bitmap) bitmap.close();

  return canvas.toDataURL("image/jpeg", QUALITY);
}

/**
 * Hochkant aufgenommene Fotos tragen ihre Drehung nur als EXIF-Angabe.
 * `imageOrientation: "from-image"` wendet sie an - ohne das läge jedes
 * Handyfoto im Profilbild auf der Seite.
 */
async function loadBitmap(file: File): Promise<ImageBitmap | HTMLImageElement> {
  if (typeof createImageBitmap === "function") {
    try {
      return await createImageBitmap(file, { imageOrientation: "from-image" });
    } catch {
      // Ältere Browser kennen die Option nicht - unten weiter.
    }
  }

  const url = URL.createObjectURL(file);
  try {
    return await new Promise<HTMLImageElement>((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error("Bild konnte nicht gelesen werden."));
      img.src = url;
    });
  } finally {
    URL.revokeObjectURL(url);
  }
}
