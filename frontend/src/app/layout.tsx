import type { Metadata, Viewport } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import { AuthProvider } from "@/lib/auth-context";
import { Toaster } from "@/components/ui/sonner";
import { PwaRegister } from "@/components/pwa-register";
import { PwaInstallPrompt } from "@/components/pwa-install-prompt";
import { ChunkErrorReloader } from "@/components/chunk-error-reloader";

// "Premium Sleek Modern"-Markenschrift; als CSS-Variable --font-jakarta
// bereitgestellt und in globals.css an --font-sans/--font-heading gebunden.
const jakarta = Plus_Jakarta_Sans({
  variable: "--font-jakarta",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700", "800"],
  display: "swap",
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
            {children}
            <Toaster />
            <PwaRegister />
            <PwaInstallPrompt />
            <ChunkErrorReloader />
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
