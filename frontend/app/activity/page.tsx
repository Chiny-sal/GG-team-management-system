"use client";

import { useCallback, useEffect, useState } from "react";
import { MemberLink } from "@/components/MemberLink";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { ActivityLog } from "@/lib/types";

const PAGE_SIZE = 20;

export default function ActivityPage() {
  const { user } = useAuth();
  const [items, setItems] = useState<ActivityLog[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!user) return;
    api
      .activity(page, PAGE_SIZE, search)
      .then((result) => {
        setItems(result.items);
        setTotal(result.total);
      })
      .catch((e: Error) => setError(e.message));
  }, [user, page, search]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Live ledger</p>
        <h1 className="mt-1 text-4xl">Activity</h1>
      </div>
      <input
        className="field max-w-md"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
        placeholder="Search activity…"
      />
      {error && <p className="text-sm text-clay">{error}</p>}
      <ol className="space-y-3">
        {items.map((entry) => (
          <li key={entry.id} className="card px-5 py-4">
            <p className="font-medium">{entry.summary}</p>
            <p className="mt-1 text-xs text-muted">
              {entry.changedByMemberId ? (
                <>
                  <MemberLink id={entry.changedByMemberId} name={entry.changedByName ?? "Member"} />
                  {" · "}
                </>
              ) : null}
              {new Date(entry.occurredAt).toLocaleString()}
            </p>
          </li>
        ))}
        {items.length === 0 && <p className="text-muted">No activity recorded yet.</p>}
      </ol>
      <div className="flex items-center gap-3 text-sm">
        <button
          disabled={page <= 1}
          onClick={() => setPage((p) => p - 1)}
          className="btn-secondary disabled:opacity-40"
        >
          Previous
        </button>
        <span>
          Page {page} of {pages}
        </span>
        <button
          disabled={page >= pages}
          onClick={() => setPage((p) => p + 1)}
          className="btn-secondary disabled:opacity-40"
        >
          Next
        </button>
      </div>
    </div>
  );
}
