"use client";

import { useState } from "react";

export function CreateGroupDialog({
  busy = false,
  error,
  onCancel,
  onCreate,
}: {
  busy?: boolean;
  error?: string | null;
  onCancel: () => void;
  onCreate: (name: string) => Promise<void> | void;
}) {
  const [name, setName] = useState("");

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) return;
    await onCreate(trimmed);
  }

  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-ink/30 px-4" onClick={onCancel}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-group-title"
        className="card w-full max-w-md p-6"
        onClick={(event) => event.stopPropagation()}
        onSubmit={(event) => void submit(event)}
      >
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-teal">New group</p>
        <h2 id="create-group-title" className="mt-1 text-2xl">
          Create a sub-group
        </h2>
        <label className="mt-5 block text-sm font-medium">
          Group name
          <input
            className="field mt-1"
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={200}
            required
            autoFocus
          />
        </label>
        {error && <p className="mt-3 text-sm text-clay">{error}</p>}
        <div className="mt-6 flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onCancel} disabled={busy}>
            Cancel
          </button>
          <button type="submit" className="btn-primary" disabled={busy || !name.trim()}>
            {busy ? "Creating…" : "Create group"}
          </button>
        </div>
      </form>
    </div>
  );
}
