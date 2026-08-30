"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { Dashboard } from "@/lib/types";

export default function DashboardPage() {
  const { user, isLead } = useAuth();
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!user) return;
    api.dashboard().then(setData).catch((e: Error) => setError(e.message));
  }, [user]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  async function promote(id: string) {
    setError(null);
    try {
      await api.promoteSuggestion(id);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not promote suggestion.");
    }
  }

  if (!data) return <p className="text-muted">{error ?? "Loading dashboard…"}</p>;

  return (
    <div className="space-y-8">
      <div>
        <p className="text-xs uppercase tracking-[0.2em] text-teal">This week</p>
        <h1 className="serif text-4xl">Dashboard</h1>
      </div>

      {error && <p className="text-sm text-clay">{error}</p>}

      <section className="rounded-3xl border border-line bg-card p-6">
        <p className="text-xs uppercase tracking-[0.2em] text-muted">Current meeting topic</p>
        <h2 className="serif mt-2 text-3xl">
          {data.currentMeeting?.topicText ?? "No topic has been promoted yet."}
        </h2>
        {data.currentMeeting && (
          <p className="mt-2 text-sm text-muted">Scheduled {data.currentMeeting.scheduledDate}</p>
        )}

        {isLead && (
          <div className="mt-6">
            <h3 className="font-medium">Promote from suggestions</h3>
            {data.pendingSuggestions.length === 0 ? (
              <p className="mt-2 text-sm text-muted">No pending Telegram suggestions.</p>
            ) : (
              <ul className="mt-3 space-y-2">
                {data.pendingSuggestions.map((suggestion) => (
                  <li
                    key={suggestion.id}
                    className="flex items-start justify-between gap-4 rounded-2xl border border-line bg-paper px-4 py-3"
                  >
                    <div>
                      <p>{suggestion.text}</p>
                      <p className="text-xs text-muted">
                        {suggestion.submittedByName} · {new Date(suggestion.submittedAt).toLocaleString()}
                      </p>
                    </div>
                    <button
                      onClick={() => promote(suggestion.id)}
                      className="shrink-0 rounded-full bg-teal px-3 py-1 text-sm text-white"
                    >
                      Promote
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {data.groupSummaries.map((group) => (
          <Link
            key={group.groupId}
            href={`/board/${group.groupId}`}
            className="rounded-3xl border border-line bg-card p-5 transition hover:border-teal"
          >
            <h3 className="serif text-2xl">{group.groupName}</h3>
            <dl className="mt-4 grid grid-cols-2 gap-2 text-sm">
              <Stat label="Assigned" value={group.assignedCount} />
              <Stat label="Ongoing" value={group.ongoingCount} />
              <Stat label="Done" value={group.doneCount} />
              <Stat label="Not done" value={group.notDoneCount} />
              <Stat label="Unassigned" value={group.unassignedCount} />
            </dl>
          </Link>
        ))}
      </section>

      <div className="grid gap-6 lg:grid-cols-3">
        <WorkList title="Work done" items={data.doneThisWeek} />
        <WorkList title="Not done" items={data.notDoneThisWeek} />
        <WorkList title="Assigned this week" items={data.assignedThisWeek} />
      </div>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <dt className="text-muted">{label}</dt>
      <dd className="text-lg font-medium">{value}</dd>
    </div>
  );
}

function WorkList({
  title,
  items,
}: {
  title: string;
  items: Dashboard["doneThisWeek"];
}) {
  return (
    <section className="rounded-3xl border border-line bg-card p-5">
      <h3 className="serif text-2xl">{title}</h3>
      {items.length === 0 ? (
        <p className="mt-3 text-sm text-muted">Nothing here yet.</p>
      ) : (
        <ul className="mt-3 space-y-2 text-sm">
          {items.map((item) => (
            <li key={item.id} className="rounded-xl bg-paper px-3 py-2">
              <p className="font-medium">{item.title}</p>
              <p className="text-muted">
                {item.assignedMemberName ?? "Unassigned"} · {item.status} · due {item.deadline}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
