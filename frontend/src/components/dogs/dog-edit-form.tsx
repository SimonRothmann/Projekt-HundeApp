"use client";

import { useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import { clearCachedData } from "@/lib/read-cache";
import { fileToSquareDataUrl } from "@/lib/image-resize";
import { formatDogAge } from "@/lib/dog-age";
import type { Dog, DogGender } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { Camera, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
/**
 * Stammdaten eines Hundes ändern.
 *
 * Bis hierher ließ sich ein Hund nur anlegen und löschen - ein Tippfehler im
 * Namen oder ein nachgetragenes Geburtsdatum bedeutete: löschen und neu
 * anlegen, samt aller Trainings. Der Endpunkt dafür gab es längst, nur die
 * Oberfläche fehlte.
 */
export function DogEditForm({
  dog,
  onSaved,
  onCancel,
}: {
  dog: Dog;
  onSaved: () => Promise<void>;
  onCancel: () => void;
}) {
  const t = useT();
  const [name, setName] = useState(dog.name);
  const [breed, setBreed] = useState(dog.breed ?? "");
  const [gender, setGender] = useState<DogGender>(dog.gender);
  const [birthday, setBirthday] = useState(dog.birthday?.slice(0, 10) ?? "");
  const [notes, setNotes] = useState(dog.notes ?? "");
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  // Steigt bei jedem Bildwechsel und zwingt das Vorschaubild zum Neuladen -
  // der Hund selbst ändert sich dabei nicht, React sähe sonst keinen Grund.
  const [imageVersion, setImageVersion] = useState(0);
  const [hasImage, setHasImage] = useState(dog.hasImage);
  const fileInput = useRef<HTMLInputElement>(null);

  const age = formatDogAge(birthday);

  async function save(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await api.put(`/api/dogs/${dog.id}`, {
        name: name.trim(),
        breed: breed.trim() || null,
        birthday: birthday || null,
        gender,
        imageUrl: dog.imageUrl,
        notes: notes.trim() || null,
      });
      toast.success("Gespeichert.");
      await onSaved();
      onCancel();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  async function pickImage(file: File | undefined) {
    if (!file) return;
    setUploading(true);
    try {
      // Erst verkleinern, dann hochladen - siehe fileToSquareDataUrl.
      const dataUrl = await fileToSquareDataUrl(file);
      await api.put(`/api/dogs/${dog.id}/image`, { dataUrl });
      // Zwischengespeichertes altes Bild verwerfen, sonst zeigt die Liste
      // weiter das vorherige.
      await clearCachedData(`dog-image-${dog.id}`);
      setHasImage(true);
      setImageVersion((v) => v + 1);
      toast.success(t("Bild gespeichert."));
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Bild konnte nicht gespeichert werden."));
    } finally {
      setUploading(false);
      // Zurücksetzen, damit dieselbe Datei erneut gewählt werden kann.
      if (fileInput.current) fileInput.current.value = "";
    }
  }

  async function removeImage() {
    setUploading(true);
    try {
      await api.delete(`/api/dogs/${dog.id}/image`);
      await clearCachedData(`dog-image-${dog.id}`);
      setHasImage(false);
      setImageVersion((v) => v + 1);
      toast.success("Bild entfernt.");
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Bild konnte nicht entfernt werden."));
    } finally {
      setUploading(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("Hund bearbeiten")}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={save} className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <DogAvatar
              key={imageVersion}
              dogId={dog.id}
              hasImage={hasImage}
              name={dog.name}
              className="size-20"
              iconClassName="size-9"
            />
            <div className="flex min-w-0 flex-col gap-1.5">
              <input
                ref={fileInput}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={(e) => pickImage(e.target.files?.[0])}
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={uploading}
                onClick={() => fileInput.current?.click()}
              >
                <Camera className="size-4" />
                {uploading ? "Moment…" : hasImage ? t("Bild ändern") : t("Bild auswählen")}
              </Button>
              {hasImage && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="text-destructive hover:text-destructive"
                  disabled={uploading}
                  onClick={removeImage}
                >
                  <Trash2 className="size-4" />
{t("Bild entfernen")}
                </Button>
              )}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-name">Name</Label>
              <Input id="edit-name" required value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-breed">Rasse</Label>
              <Input id="edit-breed" value={breed} onChange={(e) => setBreed(e.target.value)} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-gender">Geschlecht</Label>
              <Select value={gender} onValueChange={(value) => setGender(value as DogGender)}>
                <SelectTrigger id="edit-gender">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={0}>{t("Rüde")}</SelectItem>
                  <SelectItem value={1}>{t("Hündin")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="edit-birthday">Geburtsdatum</Label>
              <Input
                id="edit-birthday"
                type="date"
                value={birthday}
                onChange={(e) => setBirthday(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                {age ? `Alter: ${age}` : t("Daraus wird das Alter berechnet.")}
              </p>
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="edit-notes">{t("Notizen")}</Label>
            <Input id="edit-notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={saving}>
              {saving ? t("Wird gespeichert…") : t("Speichern")}
            </Button>
            <Button type="button" variant="ghost" onClick={onCancel}>
{t("Abbrechen")}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
