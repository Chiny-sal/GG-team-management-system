"use client";

import { useState } from "react";

export function AddMemberDialog({
  groupName,
  onClose,
  onSubmit,
}: {
  groupName: string;
  onClose: () => void;
  onSubmit: (payload: { name: string; email: string; password: string }) => Promise<void>;
}) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await onSubmit({ name: name.trim(), email: email.trim(), password });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not add member.");
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-ink/30 px-4">
      <form onSubmit={submit} className="card w-full max-w-md p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-teal">New member</p>
        <h2 className="mt-1 text-2xl">Add to {groupName}</h2>
        <p className="mt-2 text-sm text-muted">
          Creates a member on this board and a login account. Password needs 8+ characters, with upper, lower, and a
          number.
        </p>
        <label className="mt-5 block text-sm font-medium">
          Name
          <input className="field mt-1" value={name} onChange={(e) => setName(e.target.value)} required />
        </label>
        <label className="mt-3 block text-sm font-medium">
          Email
          <input
            type="email"
            className="field mt-1"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </label>
        <label className="mt-3 block text-sm font-medium">
          Password
          <input
            type="password"
            className="field mt-1"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
          />
        </label>
        {error && <p className="mt-3 text-sm text-clay">{error}</p>}
        <div className="mt-6 flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button type="submit" className="btn-primary" disabled={busy || !name.trim()}>
            {busy ? "Adding…" : "Add member"}
          </button>
        </div>
      </form>
    </div>
  );
}
