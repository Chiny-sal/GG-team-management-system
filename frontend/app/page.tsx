"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { PeriodToggle } from "@/components/PeriodToggle";
import { api, dueLabel } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { Dashboard, TimePeriod } from "@/lib/types";

export default function DashboardPage() {
  const { user, isLead } = useAuth();
  const [period, setPeriod] = useState<TimePeriod>("week");
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [topicText, setTopicText] = useState("");
  const [addingTopic, setAddingTopic] = useState(false);

  const load = useCallback(() => {
    if (!user) return;
    api.dashboard(period).then(setData).catch((e: Error) => setError(e.message));
  }, [user, period]);

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

  async function addTopic(event: React.FormEvent) {
    event.preventDefault();
    if (!topicText.trim()) return;
    setAddingTopic(true);
    setError(null);
    try {
      await api.addSuggestion(topicText.trim());
      setTopicText("");
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not add topic.");
    } finally {
      setAddingTopic(false);
    }
  }

  if (!data) return <p className="text-muted">{error ?? "Loading dashboard…"}</p>;

  return (
    <div className="space-y-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">{data.periodLabel ?? "This week"}</p>
          <h1 className="mt-1 text-4xl">Dashboard</h1>
        </div>
        <PeriodToggle value={period} onChange={setPeriod} />
      </div>

      {error && <p className="text-sm text-clay">{error}</p>}

      {isLead && (
        <section className="card p-6">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Add topic</p>
          <h2 className="mt-2 text-2xl">Suggest a meeting topic</h2>
          <p className="mt-1 text-sm text-muted">
            Adds to the same suggestions list as Telegram submissions, so you can promote it below.
          </p>
          <form onSubmit={addTopic} className="mt-4 flex flex-wrap gap-2">
            <input
              value={topicText}
              onChange={(e) => setTopicText(e.target.value)}
              placeholder="Topic title or text…"
              className="field min-w-56 flex-1"
            />
            <button disabled={addingTopic || !topicText.trim()} className="btn-primary disabled:opacity-50">
              {addingTopic ? "Adding…" : "Add topic"}
            </button>
          </form>
        </section>
      )}

      <section className="card p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Current meeting topic</p>
        <h2 className="mt-2 text-3xl">
          {data.currentMeeting?.topicText ?? "No topic has been promoted yet."}
        </h2>
        {data.currentMeeting && (
          <p className="mt-2 text-sm text-muted">Scheduled {data.currentMeeting.scheduledDate}</p>
        )}

        {isLead && (
          <div className="mt-6">
            <h3 className="font-semibold">Promote from suggestions</h3>
            {data.pendingSuggestions.length === 0 ? (
              <p className="mt-2 text-sm text-muted">No pending suggestions.</p>
            ) : (
              <ul className="mt-3 space-y-2">
                {data.pendingSuggestions.map((suggestion) => (
                  <li key={suggestion.id} className="flex items-start justify-between gap-4 rounded-2xl bg-paper px-4 py-3">
                    <div>
                      <p>{suggestion.text}</p>
                      <p className="text-xs text-muted">
                        {suggestion.submittedByName} · {new Date(suggestion.submittedAt).toLocaleString()}
                      </p>
                    </div>
                    <button onClick={() => promote(suggestion.id)} className="btn-primary shrink-0">
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
          <Link key={group.groupId} href={`/board/${group.groupId}`} className="card p-5 transition hover:ring-2 hover:ring-teal">
            <h3 className="text-2xl">{group.groupName}</h3>
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
        <WorkList title="Work done" items={data.doneItems ?? []} />
        <WorkList title="Not done" items={data.notDoneItems ?? []} />
        <WorkList title="Assigned" items={data.assignedItems ?? []} />
      </div>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <dt className="text-muted">{label}</dt>
      <dd className="text-lg font-semibold text-teal">{value}</dd>
    </div>
  );
}

function WorkList({
  title,
  items,
}: {
  title: string;
  items: Dashboard["doneItems"];
}) {
  return (
    <section className="card p-5">
      <h3 className="text-2xl">{title}</h3>
      {items.length === 0 ? (
        <p className="mt-3 text-sm text-muted">Nothing here yet.</p>
      ) : (
        <ul className="mt-3 space-y-2 text-sm">
          {items.map((item) => {
            const due = dueLabel(item.deadline);
            return (
              <li key={item.id} className="rounded-xl bg-paper px-3 py-2">
                <p className="font-semibold">{item.title}</p>
                <p className="text-muted">
                  {[item.assignedMemberName ?? "Unassigned", item.status, due].filter(Boolean).join(" · ")}
                </p>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
