"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { PendingFeedback } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { MessageSquare } from "lucide-react";
import { toast } from "sonner";

export function PendingFeedbackSection() {
  const [items, setItems] = useState<PendingFeedback[] | null>(null);
  const [openSessionId, setOpenSessionId] = useState<string | null>(null);
  const [text, setText] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function load() {
    try {
      const data = await api.get<PendingFeedback[]>("/api/trainings/pending-feedback");
      setItems(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Liste konnte nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, []);

  function startFeedback(sessionId: string) {
    setOpenSessionId(sessionId);
    setText("");
  }

  async function handleSubmit(sessionId: string, e: React.FormEvent) {
    e.preventDefault();
    if (!text.trim()) return;
    setSubmitting(true);
    try {
      await api.put(`/api/trainings/${sessionId}/feedback`, { feedback: text });
      toast.success("Feedback gespeichert.");
      setOpenSessionId(null);
      await load();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Feedback konnte nicht gespeichert werden.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <MessageSquare className="size-5" />
          Offenes Feedback
        </CardTitle>
      </CardHeader>
      <CardContent>
        {items === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : items.length === 0 ? (
          <p className="text-sm text-muted-foreground">Kein offenes Feedback - alle betreuten Trainings sind kommentiert.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {items.map((item) => (
              <li key={item.sessionId} className="rounded-md border px-3 py-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-sm">
                    <span className="font-medium">{item.dogName}</span> ({item.ownerName}) -{" "}
                    {new Date(item.date).toLocaleDateString("de-DE")}, {item.durationMinutes} Min.
                  </span>
                  {openSessionId !== item.sessionId && (
                    <Button size="sm" variant="outline" onClick={() => startFeedback(item.sessionId)}>
                      Feedback geben
                    </Button>
                  )}
                </div>
                {openSessionId === item.sessionId && (
                  <form onSubmit={(e) => handleSubmit(item.sessionId, e)} className="mt-2 flex flex-col gap-2">
                    <textarea
                      className="min-h-16 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
                      value={text}
                      onChange={(e) => setText(e.target.value)}
                      placeholder="Rückmeldung zu diesem Training…"
                      autoFocus
                    />
                    <div className="flex gap-2 self-start">
                      <Button type="submit" size="sm" disabled={submitting}>
                        Speichern
                      </Button>
                      <Button type="button" size="sm" variant="ghost" onClick={() => setOpenSessionId(null)}>
                        Abbrechen
                      </Button>
                    </div>
                  </form>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
