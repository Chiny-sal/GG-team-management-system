"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { COLD_START_HINT, COLD_START_HINT_MS } from "@/lib/api";
import { useAuth } from "@/lib/auth";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    if (!busy) {
      setSlow(false);
      return;
    }
    const timer = window.setTimeout(() => setSlow(true), COLD_START_HINT_MS);
    return () => window.clearTimeout(timer);
  }, [busy]);

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
      router.replace("/");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sign-in failed.");
    } finally {
      setBusy(false);
    }
  }

  const status = error ?? (slow ? COLD_START_HINT : null);

  return (
    <div className="grid min-h-screen place-items-center px-6">
      <form onSubmit={onSubmit} className="card w-full max-w-md p-8">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">GG Team</p>
        <h1 className="mt-2 text-4xl">Sign in</h1>
        <label className="mt-6 block text-sm font-medium">
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="field mt-1"
            required
          />
        </label>
        <label className="mt-4 block text-sm font-medium">
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="field mt-1"
            required
          />
        </label>
        {status && (
          <p className={`mt-3 text-sm ${error ? "text-clay" : "text-muted"}`}>{status}</p>
        )}
        <button disabled={busy} className="btn-primary mt-6 w-full rounded-xl py-2.5 disabled:opacity-50">
          {busy ? "Signing in…" : "Enter"}
        </button>
      </form>
    </div>
  );
}
