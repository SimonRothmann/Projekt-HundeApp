import type { Metadata, Viewport } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import { AuthProvider } from "@/lib/auth-context";
import { PreferencesProvider } from "@/lib/preferences-context";
import { SprachProvider } from "@/lib/i18n/provider";
import { Toaster } from "@/components/ui/sonner";
import { PwaRegister } from "@/components/pwa-register";
import { PwaInstallPrompt } from "@/components/pwa-install-prompt";
import { ChunkErrorReloader } from "@/components/chunk-error-reloader";
import { SITE } from "@/lib/seo";

// "Premium Sleek Modern"-Markenschrift; als CSS-Variable --font-jakarta
// bereitgestellt und in globals.css an --font-sans/--font-heading gebunden.
const jakarta = Plus_Jakarta_Sans({
  variable: "--font-jakarta",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700", "800"],
  display: "swap",
});

export const metadata: Metadata = {
  // Pflicht, sobald relative Bild-/Canonical-Pfade verwendet werden - ohne
  // metadataBase bricht der Build (siehe Next-Doku, generate-metadata).
  metadataBase: new URL(SITE.url),

  title: {
    // Unterseiten setzen nur ihren eigenen Titel, die Marke hängt sich an.
    default: "Dogity – Trainingstagebuch für den Hundesport",
    template: "%s | Dogity",
  },
  description: SITE.description,
  applicationName: SITE.name,

  // Nach diesen Begriffen wird tatsächlich gesucht. Keywords wiegen bei Google
  // seit Jahren nichts mehr; sie stehen hier für die übrigen Suchmaschinen und
  // kosten nichts.
  keywords: [
    "Hundesport App",
    "Trainingstagebuch Hund",
    "Fährtenarbeit",
    "Fährte aufzeichnen GPS",
    "IGP Training",
    "Begleithundeprüfung",
    "IBGH",
    "Prüfungsordnung Hundesport",
    "Hundeverein Software",
    "Trainingsplan Hund",
  ],

  alternates: { canonical: "/" },

  openGraph: {
    type: "website",
    locale: SITE.locale,
    url: SITE.url,
    siteName: SITE.name,
    title: "Dogity – Trainingstagebuch für den Hundesport",
    description: SITE.description,
  },
  twitter: {
    card: "summary_large_image",
    title: "Dogity – Trainingstagebuch für den Hundesport",
    description: SITE.description,
  },

  manifest: "/manifest.webmanifest",
  icons: {
    // iOS liest apple-touch-icon für "Zum Home-Bildschirm" - SVG wird von
    // Safari ab iOS 16 unterstützt. Für ältere iOS-Versionen wäre ein
    // 180×180 PNG besser, aber SVG ist ausreichend für aktuelle Geräte.
    apple: "/icon.svg",
  },
};

export const viewport: Viewport = {
  themeColor: "#0b0f1a",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="de"
      className={`${jakarta.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <body className="min-h-full flex flex-col">
        <ThemeProvider attribute="class" defaultTheme="dark" enableSystem disableTransitionOnChange>
          <AuthProvider>
            <PreferencesProvider>
              {/* Innerhalb der Einstellungen, weil die Sprache von dort
                  kommt - und um alles herum, weil sie überall gilt. */}
              <SprachProvider>
                {children}
                <Toaster />
                <PwaRegister />
                <PwaInstallPrompt />
                <ChunkErrorReloader />
              </SprachProvider>
            </PreferencesProvider>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
