import type { Metadata } from "next";
import { IBM_Plex_Sans, Source_Serif_4 } from "next/font/google";
import { AuthProvider } from "@/lib/auth";
import { AppShell } from "@/components/AppShell";
import "./globals.css";

const ibm = IBM_Plex_Sans({
  variable: "--font-ibm",
  subsets: ["latin"],
  weight: ["400", "500", "600"],
});

const source = Source_Serif_4({
  variable: "--font-source",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "GG Activity",
  description: "Community activity tracking for weekly work, meetings, and follow-through.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${ibm.variable} ${source.variable} h-full antialiased`}>
      <body className="min-h-full">
        <AuthProvider>
          <AppShell>{children}</AppShell>
        </AuthProvider>
      </body>
    </html>
  );
}
