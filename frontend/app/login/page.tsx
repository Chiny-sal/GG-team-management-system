"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useAuth } from "@/lib/auth";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("lead@gg.local");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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

  return (
    <div className="grid min-h-screen place-items-center px-6">
      <form onSubmit={onSubmit} className="w-full max-w-md rounded-3xl border border-line bg-card p-8 shadow-sm">
        <p className="text-xs uppercase tracking-[0.2em] text-teal">Community ledger</p>
        <h1 className="serif mt-2 text-4xl">Sign in</h1>
        <p className="mt-2 text-sm text-muted">
          Leads can edit every board. Members can move their own cards.
        </p>
        <label className="mt-6 block text-sm">
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded-xl border border-line bg-paper px-3 py-2"
            required
          />
        </label>
        <label className="mt-4 block text-sm">
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded-xl border border-line bg-paper px-3 py-2"
            required
          />
        </label>
        {error && <p className="mt-3 text-sm text-clay">{error}</p>}
        <button
          disabled={busy}
          className="mt-6 w-full rounded-xl bg-teal py-2.5 text-white disabled:opacity-50"
        >
          {busy ? "Signing in…" : "Enter"}
        </button>
      </form>
    </div>
  );
}
