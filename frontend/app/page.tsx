"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { PeriodToggle } from "@/components/PeriodToggle";
import { WorkItemDetailModal } from "@/components/WorkItemDetailModal";
import { MemberLink } from "@/components/MemberLink";
import { api, dueLabel } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { currentPeriodSelection, type PeriodSelection } from "@/lib/period";
import { useLiveReload } from "@/lib/useLiveReload";
import type { Dashboard, Meeting, PastMeeting, PastMeetingDay, WorkItem } from "@/lib/types";

export default function DashboardPage() {
  const { user, isLead } = useAuth();
  const [period, setPeriod] = useState<PeriodSelection>(currentPeriodSelection);
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [topicText, setTopicText] = useState("");
  const [addingTopic, setAddingTopic] = useState(false);
  const [selectedWorkId, setSelectedWorkId] = useState<string | null>(null);

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

  const selectedWork =
    selectedWorkId &&
    [...(data.doneItems ?? []), ...(data.notDoneItems ?? []), ...(data.assignedItems ?? [])].find(
      (item) => item.id === selectedWorkId,
    );

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
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">This week&apos;s meeting topic</p>
        <h2 className="mt-2 text-3xl">
          {data.currentMeeting?.topicText ?? "No topic has been promoted yet."}
        </h2>
        {data.currentMeeting && (
          <p className="mt-2 text-sm text-muted">Scheduled {data.currentMeeting.scheduledDate}</p>
        )}
        {data.currentMeeting && (
          <MeetingNotes
            meeting={data.currentMeeting}
            canEdit={isLead}
            onSaved={load}
            onError={setError}
          />
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

      <PastMeetings
        days={data.pastMeetings ?? []}
        canEdit={isLead}
        onSaved={load}
        onError={setError}
      />

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
        <WorkList title="Work done" items={data.doneItems ?? []} onOpen={setSelectedWorkId} />
        <WorkList title="Not done" items={data.notDoneItems ?? []} onOpen={setSelectedWorkId} />
        <WorkList title="Assigned" items={data.assignedItems ?? []} onOpen={setSelectedWorkId} />
      </div>

      {selectedWorkId && (
        <WorkItemDetailModal
          workItemId={selectedWorkId}
          fallback={selectedWork || undefined}
          onClose={() => setSelectedWorkId(null)}
        />
      )}
    </div>
  );
}

function MeetingNotes({
  meeting,
  canEdit,
  onSaved,
  onError,
  compact = false,
}: {
  meeting: Pick<Meeting, "id" | "notes">;
  canEdit: boolean;
  onSaved: () => void;
  onError: (message: string | null) => void;
  compact?: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [notes, setNotes] = useState(meeting.notes ?? "");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!editing) setNotes(meeting.notes ?? "");
  }, [meeting.notes, meeting.id, editing]);

  async function save() {
    setSaving(true);
    onError(null);
    try {
      await api.updateMeetingNotes(meeting.id, notes.trim() || null);
      setEditing(false);
      onSaved();
    } catch (e) {
      onError(e instanceof Error ? e.message : "Could not save notes.");
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <div className={compact ? "mt-3" : "mt-4"}>
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          maxLength={4000}
          rows={compact ? 3 : 4}
          placeholder="What was decided or discussed…"
          className="field"
        />
        <div className="mt-2 flex flex-wrap gap-2">
          <button type="button" disabled={saving} onClick={save} className="btn-primary disabled:opacity-50">
            {saving ? "Saving…" : "Save notes"}
          </button>
          <button
            type="button"
            disabled={saving}
            onClick={() => {
              setNotes(meeting.notes ?? "");
              setEditing(false);
            }}
            className="btn-secondary"
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={compact ? "mt-3" : "mt-4"}>
      {meeting.notes ? (
        <p className="whitespace-pre-wrap text-sm text-ink/80">{meeting.notes}</p>
      ) : (
        !canEdit && !compact && <p className="text-sm text-muted">No notes yet.</p>
      )}
      {canEdit && (
        <button type="button" onClick={() => setEditing(true)} className="btn-secondary mt-3">
          {meeting.notes ? "Edit notes" : "Add notes"}
        </button>
      )}
    </div>
  );
}

function PastMeetings({
  days,
  canEdit,
  onSaved,
  onError,
}: {
  days: PastMeetingDay[];
  canEdit: boolean;
  onSaved: () => void;
  onError: (message: string | null) => void;
}) {
  const [query, setQuery] = useState("");
  const [expanded, setExpanded] = useState(true);
  const needle = query.trim().toLowerCase();
  const filtered = needle
    ? days
        .map((day) => ({
          ...day,
          meetings: day.meetings.filter((meeting) => meetingMatches(meeting, needle)),
        }))
        .filter((day) => day.meetings.length > 0)
    : days;

  return (
    <details
      className="card p-6"
      open={expanded}
      onToggle={(event) => setExpanded(event.currentTarget.open)}
    >
      <summary className="cursor-pointer">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">History</p>
        <h2 className="mt-2 text-2xl">Past meetings</h2>
        <p className="mt-1 text-sm text-muted">
          Previous topics stay here after a new one is promoted, grouped by date when more than one topic was promoted
          that day.
        </p>
      </summary>

      {days.length === 0 ? (
        <p className="mt-4 text-sm text-muted">No past meetings yet. Promote a new topic to archive the current one.</p>
      ) : (
        <>
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search by date, topic, notes, or work item…"
            className="field mt-4"
            aria-label="Search past meetings"
          />
          <ul className="mt-4 max-h-[28rem] space-y-4 overflow-y-auto pr-1">
            {filtered.length === 0 ? (
              <li className="text-sm text-muted">No meetings match that search.</li>
            ) : (
              filtered.map((day) => (
                <li key={day.scheduledDate} className="rounded-2xl bg-paper px-4 py-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-teal">{day.scheduledDate}</p>
                  <ul className="mt-3 space-y-4">
                    {day.meetings.map((meeting) => (
                      <li key={meeting.id} className="border-t border-line/80 pt-3 first:border-0 first:pt-0">
                        <p className="font-semibold">{meeting.topicText ?? "Untitled meeting"}</p>
                        <MeetingNotes
                          meeting={meeting}
                          canEdit={canEdit}
                          onSaved={onSaved}
                          onError={onError}
                          compact
                        />
                        {meeting.workItems.length > 0 && (
                          <div className="mt-3">
                            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
                              Work from this meeting
                            </p>
                            <ul className="mt-1 space-y-1 text-sm">
                              {meeting.workItems.map((item) => (
                                <li key={item.id}>
                                  {item.title}
                                  <span className="text-muted">
                                    {" · "}
                                    {item.assignedMemberId ? (
                                      <MemberLink id={item.assignedMemberId} name={item.assignedMemberName} />
                                    ) : (
                                      "Unassigned"
                                    )}
                                    {" · "}
                                    {item.status}
                                  </span>
                                </li>
                              ))}
                            </ul>
                          </div>
                        )}
                      </li>
                    ))}
                  </ul>
                </li>
              ))
            )}
          </ul>
        </>
      )}
    </details>
  );
}

function meetingMatches(meeting: PastMeeting, needle: string) {
  const haystack = [
    meeting.scheduledDate,
    meeting.topicText,
    meeting.notes,
    ...meeting.workItems.map((item) => item.title),
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
  return haystack.includes(needle);
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
  onOpen,
}: {
  title: string;
  items: WorkItem[];
  onOpen: (id: string) => void;
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
                <button type="button" onClick={() => onOpen(item.id)} className="w-full text-left">
                  <p className="font-semibold">{item.title}</p>
                </button>
                <p className="text-muted">
                  {item.assignedMemberId ? (
                    <MemberLink id={item.assignedMemberId} name={item.assignedMemberName} />
                  ) : (
                    "Unassigned"
                  )}
                  {` · ${item.status}`}
                  {due ? ` · ${due}` : ""}
                </p>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
