"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { MemberLink } from "@/components/MemberLink";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { Notification, NotificationGroup } from "@/lib/types";

const LABELS: Record<string, string> = {
  MemberNoAssignmentTwoWeeks: "No assignment in two weeks",
  WorkNotDoneTwoWeeks: "Work not done after two weeks",
  WorkItemAssigned: "Work assigned",
  MissedLastTwoMeetings: "Missed last two meetings",
};

export default function NotificationsPage() {
  const { user } = useAuth();
  const [groups, setGroups] = useState<NotificationGroup[]>([]);
  const [canMarkRead, setCanMarkRead] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const load = useCallback(() => {
    if (!user) return;
    api
      .notifications()
      .then((page) => {
        setGroups(page.groups);
        setCanMarkRead(page.canMarkRead);
      })
      .catch((e: Error) => setError(e.message));
  }, [user]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  const filtered = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return groups;
    return groups
      .map((group) => ({
        ...group,
        items: group.items.filter((item) =>
          [
            item.memberName,
            item.workItemTitle,
            item.workItemDescription,
            item.workItemDeadline,
            item.groupName,
            LABELS[group.type] ?? group.type,
            item.assignmentStatus === "AssignedWork" ? "Assigned work" : "",
            item.assignmentStatus === "NoAssignment" ? "No assignment in two weeks" : "",
            item.attendanceStatus === "MissedLastTwo" ? "Missed last two meetings" : "",
            item.attendanceStatus === "AttendedSince" ? "Attended since" : "",
          ]
            .filter(Boolean)
            .join(" ")
            .toLowerCase()
            .includes(needle),
        ),
      }))
      .filter((group) => group.items.length > 0);
  }, [groups, search]);

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
          <h1 className="mt-1 text-3xl md:text-4xl">Notifications</h1>
        {!canMarkRead && (
          <p className="mt-2 text-sm text-muted">
            You can view your group&apos;s notifications. Marking as read is limited to Team Leads and Office Management.
          </p>
        )}
      </div>
      <input
        className="field w-full max-w-md"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search notifications…"
      />
      {error && <p className="text-sm text-clay">{error}</p>}
      {filtered.length === 0 && <p className="text-muted">No unread notifications.</p>}
      {filtered.map((group) => (
        <section key={group.type} className="card p-6">
          <h2 className="text-2xl">{LABELS[group.type] ?? group.type}</h2>
          <ul className="mt-4 space-y-3">
            {group.items.map((item) => (
              <li key={item.id} className="flex flex-col gap-3 rounded-2xl bg-paper px-4 py-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <p>
                    <MemberLink id={item.memberId} name={item.memberName ?? "Member"} />
                    {item.workItemTitle && group.type !== "WorkItemAssigned" ? ` · ${item.workItemTitle}` : ""}
                  </p>
                  <WorkItemDetails item={item} />
                  <AssignmentBadge item={item} />
                  <AttendanceBadge item={item} />
                  <p className="text-xs text-muted">{new Date(item.createdAt).toLocaleString()}</p>
                </div>
                {canMarkRead && (
                  <button onClick={() => markRead(item.id)} className="btn-secondary">
                    Mark as read
                  </button>
                )}
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function WorkItemDetails({ item }: { item: Notification }) {
  if (!item.workItemId && !item.workItemTitle) return null;

  return (
    <dl className="mt-2 space-y-1 text-sm">
      <div>
        <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Title</dt>
        <dd>{item.workItemTitle ?? "Untitled work item"}</dd>
      </div>
      {item.workItemDescription ? (
        <div>
          <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Description</dt>
          <dd className="whitespace-pre-wrap">{item.workItemDescription}</dd>
        </div>
      ) : null}
      <div>
        <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Deadline</dt>
        <dd>{item.workItemDeadline ?? "No deadline set"}</dd>
      </div>
      <div>
        <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Group</dt>
        <dd>{item.groupName ?? "Unknown group"}</dd>
      </div>
    </dl>
  );
}

function AssignmentBadge({ item }: { item: Notification }) {
  if (item.type !== "MemberNoAssignmentTwoWeeks" || !item.assignmentStatus) return null;

  const assigned = item.assignmentStatus === "AssignedWork";
  return (
    <p
      className={`mt-1 inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${
        assigned ? "bg-teal-soft text-teal" : "text-clay ring-1 ring-clay/30"
      }`}
    >
      {assigned ? "Assigned work" : "No assignment in two weeks"}
    </p>
  );
}

function AttendanceBadge({ item }: { item: Notification }) {
  if (item.type !== "MissedLastTwoMeetings" || !item.attendanceStatus) return null;

  const attended = item.attendanceStatus === "AttendedSince";
  return (
    <p
      className={`mt-1 inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${
        attended ? "bg-teal-soft text-teal" : "text-clay ring-1 ring-clay/30"
      }`}
    >
      {attended ? "Attended since" : "Missed last two meetings"}
    </p>
  );
}
