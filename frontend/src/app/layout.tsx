import type { Metadata, Viewport } from "next";
import { Geist } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import { AuthProvider } from "@/lib/auth-context";
import { Toaster } from "@/components/ui/sonner";
import { PwaRegister } from "@/components/pwa-register";
import { PwaInstallPrompt } from "@/components/pwa-install-prompt";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Dogity",
  description: "Die digitale Plattform für modernen Hundesport",
  manifest: "/manifest.webmanifest",
  icons: {
    // iOS liest apple-touch-icon für "Zum Home-Bildschirm" - SVG wird von
    // Safari ab iOS 16 unterstützt. Für ältere iOS-Versionen wäre ein
    // 180×180 PNG besser, aber SVG ist ausreichend für aktuelle Geräte.
    apple: "/icon.svg",
  },
};

export const viewport: Viewport = {
  themeColor: "#3d86f0",
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
      className={`${geistSans.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <body className="min-h-full flex flex-col">
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
          <AuthProvider>
            {children}
            <Toaster />
            <PwaRegister />
            <PwaInstallPrompt />
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
