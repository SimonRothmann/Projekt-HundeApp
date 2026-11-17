"use client"

import { useTheme } from "next-themes"
import { useAufzeichnungOffen } from "@/lib/aufzeichnung-offen"
import { Toaster as Sonner, type ToasterProps } from "sonner"
import { CircleCheckIcon, InfoIcon, TriangleAlertIcon, OctagonXIcon, Loader2Icon } from "lucide-react"

const Toaster = ({ ...props }: ToasterProps) => {
  const { theme = "system" } = useTheme()
  // Während einer GPS-Aufzeichnung oben: unten liegen Marker und Beenden (siehe lib/aufzeichnung-offen.ts).
  const aufzeichnung = useAufzeichnungOffen()

  return (
    <Sonner
      theme={theme as ToasterProps["theme"]}
      className="toaster group"
      position={aufzeichnung ? "top-center" : undefined}
      // Unter der Kopfzeile des Vollbilds, sonst lägen sie über "Sperren" und "Abbrechen".
      offset={aufzeichnung ? { top: "calc(env(safe-area-inset-top) + 76px)" } : undefined}
      mobileOffset={aufzeichnung ? { top: "calc(env(safe-area-inset-top) + 76px)" } : undefined}
      icons={{
        success: (
          <CircleCheckIcon className="size-4" />
        ),
        info: (
          <InfoIcon className="size-4" />
        ),
        warning: (
          <TriangleAlertIcon className="size-4" />
        ),
        error: (
          <OctagonXIcon className="size-4" />
        ),
        loading: (
          <Loader2Icon className="size-4 animate-spin" />
        ),
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",
          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      toastOptions={{
        classNames: {
          toast: "cn-toast",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }
