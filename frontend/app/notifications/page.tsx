"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { NotificationGroup } from "@/lib/types";

const LABELS: Record<string, string> = {
  MemberNoAssignmentTwoWeeks: "No assignment in two weeks",
  WorkNotDoneTwoWeeks: "Work not done after two weeks",
};

export default function NotificationsPage() {
  const { user } = useAuth();
  const [groups, setGroups] = useState<NotificationGroup[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!user) return;
    api.notifications().then(setGroups).catch((e: Error) => setError(e.message));
  }, [user]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  async function markRead(id: string) {
    try {
      await api.markNotificationRead(id);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not mark as read.");
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Follow-through</p>
        <h1 className="mt-1 text-4xl">Notifications</h1>
      </div>
      {error && <p className="text-sm text-clay">{error}</p>}
      {groups.length === 0 && <p className="text-muted">No unread notifications.</p>}
      {groups.map((group) => (
        <section key={group.type} className="card p-6">
          <h2 className="text-2xl">{LABELS[group.type] ?? group.type}</h2>
          <ul className="mt-4 space-y-3">
            {group.items.map((item) => (
              <li key={item.id} className="flex items-start justify-between gap-4 rounded-2xl bg-paper px-4 py-3">
                <div>
                  <p>
                    {item.memberName ?? "Member"}
                    {item.workItemTitle ? ` · ${item.workItemTitle}` : ""}
                  </p>
                  <p className="text-xs text-muted">{new Date(item.createdAt).toLocaleString()}</p>
                </div>
                <button onClick={() => markRead(item.id)} className="btn-secondary">
                  Mark as read
                </button>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
