"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { Dog } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dog as DogIcon, Plus } from "lucide-react";
import { toast } from "sonner";

export default function DogsPage() {
  const [dogs, setDogs] = useState<Dog[] | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [breed, setBreed] = useState("");
  const [gender, setGender] = useState<0 | 1>(0);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function loadDogs() {
    try {
      const data = await api.get<Dog[]>("/api/dogs");
      setDogs(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Hunde konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf beim Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDogs();
  }, []);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await api.post<Dog>("/api/dogs", {
        name,
        breed: breed || null,
        birthday: null,
        gender,
        imageUrl: null,
        notes: null,
      });
      setName("");
      setBreed("");
      setGender(0);
      setShowForm(false);
      toast.success("Hund angelegt.");
      await loadDogs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Hund konnte nicht angelegt werden.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight">Meine Hunde</h1>
        <Button onClick={() => setShowForm((v) => !v)} size="sm">
          <Plus className="size-4" />
          Hund hinzufügen
        </Button>
      </div>

      {showForm && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Neuer Hund</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleCreate} className="flex flex-col gap-4">
              <div className="grid gap-4 sm:grid-cols-3">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="name">Name</Label>
                  <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="breed">Rasse</Label>
                  <Input id="breed" value={breed} onChange={(e) => setBreed(e.target.value)} />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="gender">Geschlecht</Label>
                  <Select
                    value={gender}
                    onValueChange={(value) => setGender(value as 0 | 1)}
                  >
                    <SelectTrigger id="gender">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={0}>Rüde</SelectItem>
                      <SelectItem value={1}>Hündin</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <Button type="submit" className="self-start" disabled={isSubmitting}>
                {isSubmitting ? "Wird gespeichert…" : "Speichern"}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {dogs === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : dogs.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center text-muted-foreground">
            <DogIcon className="size-10" />
            <p>Noch keine Hunde angelegt.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {dogs.map((dog) => (
            <Link key={dog.id} href={`/dogs/${dog.id}`}>
              <Card className="transition-colors hover:bg-accent/10">
                <CardHeader className="flex-row items-center gap-3 space-y-0">
                  <div className="flex size-12 items-center justify-center rounded-full bg-secondary">
                    <DogIcon className="size-6 text-secondary-foreground" />
                  </div>
                  <div>
                    <CardTitle className="text-base">{dog.name}</CardTitle>
                    <p className="text-sm text-muted-foreground">{dog.breed ?? "Unbekannte Rasse"}</p>
                  </div>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
