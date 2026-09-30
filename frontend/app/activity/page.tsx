"use client";

import { useCallback, useEffect, useState } from "react";
import { MemberLink } from "@/components/MemberLink";
import { ShowMoreButton } from "@/components/ShowMoreButton";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { ActivityLog } from "@/lib/types";

const PAGE_SIZE = 15;

export default function ActivityPage() {
  const { user } = useAuth();
  const [items, setItems] = useState<ActivityLog[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);

  const load = useCallback(() => {
    if (!user) return;
    api
      .activity(1, PAGE_SIZE, search)
      .then((result) => {
        setItems(result.items);
        setTotal(result.total);
      })
      .catch((e: Error) => setError(e.message));
  }, [user, search]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  async function showMore() {
    const nextPage = Math.floor(items.length / PAGE_SIZE) + 1;
    setLoadingMore(true);
    setError(null);
    try {
      const result = await api.activity(nextPage, PAGE_SIZE, search);
      setItems((current) => {
        const seen = new Set(current.map((entry) => entry.id));
        return [...current, ...result.items.filter((entry) => !seen.has(entry.id))];
      });
      setTotal(result.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load more activity.");
    } finally {
      setLoadingMore(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Live ledger</p>
          <h1 className="mt-1 text-3xl md:text-4xl">Activity</h1>
      </div>
      <input
        className="field w-full max-w-md"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
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
      <ShowMoreButton remaining={total - items.length} onClick={() => void showMore()} busy={loadingMore} />
    </div>
  );
}
