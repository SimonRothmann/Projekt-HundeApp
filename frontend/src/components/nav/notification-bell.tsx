"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import type { Notification } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Popover, PopoverTrigger, PopoverContent } from "@/components/ui/popover";
import { Bell } from "lucide-react";
import { toast } from "sonner";

export function NotificationBell() {
  const { unreadNotificationCount, refreshUnreadNotificationCount } = useAuth();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState<Notification[] | null>(null);

  async function loadNotifications() {
    try {
      const data = await api.get<Notification[]>("/api/notifications");
      setNotifications(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Benachrichtigungen konnten nicht geladen werden.");
    }
  }

  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);
    if (nextOpen) loadNotifications();
  }

  async function handleClick(notification: Notification) {
    if (!notification.isRead) {
      try {
        await api.post(`/api/notifications/${notification.id}/read`);
        setNotifications((prev) => prev?.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)) ?? null);
        refreshUnreadNotificationCount();
      } catch {
        // Stiller Fehlschlag - Navigation soll trotzdem funktionieren.
      }
    }
    if (notification.linkPath) {
      setOpen(false);
      router.push(notification.linkPath);
    }
  }

  async function handleMarkAllRead() {
    try {
      await api.post("/api/notifications/read-all");
      setNotifications((prev) => prev?.map((n) => ({ ...n, isRead: true })) ?? null);
      refreshUnreadNotificationCount();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Aktion fehlgeschlagen.");
    }
  }

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger
        render={
          <Button variant="ghost" size="icon" className="relative">
            <Bell className="size-5" />
            {unreadNotificationCount > 0 && (
              <Badge variant="destructive" className="absolute -right-1 -top-1 h-4 min-w-4 px-1 text-[10px]">
                {unreadNotificationCount > 9 ? "9+" : unreadNotificationCount}
              </Badge>
            )}
          </Button>
        }
      />
      <PopoverContent>
        <div className="flex items-center justify-between border-b p-3">
          <span className="text-sm font-medium">Benachrichtigungen</span>
          {notifications && notifications.some((n) => !n.isRead) && (
            <Button variant="ghost" size="sm" onClick={handleMarkAllRead}>
              Alle gelesen
            </Button>
          )}
        </div>
        <div className="max-h-80 overflow-y-auto">
          {notifications === null ? (
            <p className="p-3 text-sm text-muted-foreground">Lädt…</p>
          ) : notifications.length === 0 ? (
            <p className="p-3 text-sm text-muted-foreground">Keine Benachrichtigungen.</p>
          ) : (
            <ul>
              {notifications.map((n) => (
                <li key={n.id}>
                  <button
                    type="button"
                    onClick={() => handleClick(n)}
                    className="flex w-full flex-col gap-0.5 border-b px-3 py-2.5 text-left text-sm last:border-b-0 hover:bg-accent/40"
                  >
                    <span className={n.isRead ? "text-muted-foreground" : "font-medium"}>{n.message}</span>
                    <span className="text-xs text-muted-foreground">
                      {new Date(n.createdAt).toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit", year: "numeric" })}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
