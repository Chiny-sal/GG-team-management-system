"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import { MemberLink } from "@/components/MemberLink";
import type { AttendanceRosterMember, AttendanceSession, AttendanceSessionSummary } from "@/lib/types";

type DraftRow = {
  present: boolean;
  attendedWeeklyClass: boolean;
  answers: Record<string, boolean>;
};

type EditorMode = "create" | "edit";

export default function AttendancePage() {
  const { user, loading, isOfficeManagement } = useAuth();
  const router = useRouter();
  const [roster, setRoster] = useState<AttendanceRosterMember[]>([]);
  const [sessions, setSessions] = useState<AttendanceSessionSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [exporting, setExporting] = useState(false);

  const [mode, setMode] = useState<EditorMode | null>(null);
  const [questionStep, setQuestionStep] = useState(false);
  const [draftDate, setDraftDate] = useState(todayIso);
  const [questionDrafts, setQuestionDrafts] = useState<string[]>([""]);
  const [questionKeys, setQuestionKeys] = useState<string[]>([]);
  const [questionLabels, setQuestionLabels] = useState<string[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [rows, setRows] = useState<Record<string, DraftRow>>({});
  const [search, setSearch] = useState("");

  const [broadcastOpen, setBroadcastOpen] = useState(false);
  const [broadcastMessage, setBroadcastMessage] = useState("");
  const [broadcastSelected, setBroadcastSelected] = useState<Set<string>>(new Set());
  const [broadcastSearch, setBroadcastSearch] = useState("");

  const canOpen = isOfficeManagement;

  useEffect(() => {
    if (!loading && user && !isOfficeManagement) router.replace("/");
  }, [loading, user, isOfficeManagement, router]);

  const load = useCallback(() => {
    if (!user || !canOpen) return;
    Promise.all([api.attendanceRoster(), api.attendanceSessions()])
      .then(([members, history]) => {
        setRoster(members);
        setSessions(history);
      })
      .catch((e: Error) => setError(e.message));
  }, [user, canOpen]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user && canOpen));

  const visibleRoster = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return roster;
    return roster.filter((member) =>
      [member.name, member.groupName].join(" ").toLowerCase().includes(needle),
    );
  }, [roster, search]);

  const broadcastVisible = useMemo(() => {
    const needle = broadcastSearch.trim().toLowerCase();
    if (!needle) return roster;
    return roster.filter((member) =>
      [member.name, member.groupName].join(" ").toLowerCase().includes(needle),
    );
  }, [roster, broadcastSearch]);

  function emptyRows(members: AttendanceRosterMember[], keys: string[]): Record<string, DraftRow> {
    const next: Record<string, DraftRow> = {};
    for (const member of members) {
      next[member.id] = {
        present: false,
        attendedWeeklyClass: false,
        answers: Object.fromEntries(keys.map((key) => [key, false])),
      };
    }
    return next;
  }

  function startTakeAttendance() {
    setError(null);
    setNotice(null);
    setMode("create");
    setQuestionStep(true);
    setDraftDate(todayIso());
    setQuestionDrafts([""]);
    setEditingId(null);
    setSearch("");
  }

  function continueFromQuestions() {
    const questions = questionDrafts.map((text) => text.trim()).filter(Boolean);
    const keys = questions.map((_, index) => String(index));
    setQuestionLabels(questions);
    setQuestionKeys(keys);
    setRows(emptyRows(roster, keys));
    setQuestionStep(false);
  }

  async function openSession(id: string) {
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      const session = await api.attendanceSession(id);
      applySession(session);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not open attendance.");
    } finally {
      setBusy(false);
    }
  }

  function applySession(session: AttendanceSession) {
    const keys = session.questions.map((q) => q.id);
    const next: Record<string, DraftRow> = {};
    for (const record of session.records) {
      next[record.memberId] = {
        present: record.present,
        attendedWeeklyClass: record.attendedWeeklyClass,
        answers: Object.fromEntries(record.answers.map((answer) => [answer.questionId, answer.value])),
      };
    }
    for (const member of roster) {
      if (!next[member.id]) {
        next[member.id] = {
          present: false,
          attendedWeeklyClass: false,
          answers: Object.fromEntries(keys.map((key) => [key, false])),
        };
      }
    }
    setMode("edit");
    setQuestionStep(false);
    setEditingId(session.id);
    setDraftDate(session.date);
    setQuestionLabels(session.questions.map((q) => q.questionText));
    setQuestionKeys(keys);
    setRows(next);
    setSearch("");
  }

  function patchRow(memberId: string, patch: Partial<DraftRow>) {
    setRows((current) => ({
      ...current,
      [memberId]: { ...current[memberId], ...patch },
    }));
  }

  function toggleAnswer(memberId: string, key: string) {
    setRows((current) => {
      const row = current[memberId];
      return {
        ...current,
        [memberId]: {
          ...row,
          answers: { ...row.answers, [key]: !row.answers[key] },
        },
      };
    });
  }

  function selectAll(field: "present" | "attendedWeeklyClass", value: boolean) {
    setRows((current) => {
      const next = { ...current };
      for (const member of visibleRoster) {
        next[member.id] = { ...next[member.id], [field]: value };
      }
      return next;
    });
  }

  function payloadRows() {
    return roster.map((member) => {
      const row = rows[member.id] ?? { present: false, attendedWeeklyClass: false, answers: {} };
      return {
        memberId: member.id,
        present: row.present,
        attendedWeeklyClass: row.attendedWeeklyClass,
        answers: row.answers,
      };
    });
  }

  async function save() {
    if (mode === "create" && questionStep) return;
    setBusy(true);
    setError(null);
    try {
      if (mode === "edit" && editingId) {
        await api.updateAttendanceSession(editingId, payloadRows());
        setNotice("Attendance updated.");
      } else {
        await api.createAttendanceSession({
          date: draftDate,
          questions: questionLabels,
          rows: payloadRows(),
        });
        setNotice("Attendance saved.");
      }
      setMode(null);
      setEditingId(null);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save attendance.");
    } finally {
      setBusy(false);
    }
  }

  async function exportHistory() {
    setExporting(true);
    setError(null);
    try {
      await api.downloadAttendanceExport();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Export failed.");
    } finally {
      setExporting(false);
    }
  }

  function openBroadcast() {
    setBroadcastOpen(true);
    setBroadcastMessage("");
    setBroadcastSearch("");
    setBroadcastSelected(new Set(roster.map((member) => member.id)));
    setError(null);
    setNotice(null);
  }

  async function sendBroadcast() {
    const ids = [...broadcastSelected];
    if (!broadcastMessage.trim() || ids.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const result = await api.sendAttendanceBotMessage(broadcastMessage.trim(), ids);
      setBroadcastOpen(false);
      setNotice(
        `Sent to ${result.sent} member${result.sent === 1 ? "" : "s"}${
          result.skipped ? ` · ${result.skipped} skipped (no Telegram)` : ""
        }.`,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not send message.");
    } finally {
      setBusy(false);
    }
  }

  if (loading || !user) {
    return <p className="text-muted">Loading attendance…</p>;
  }

  if (!canOpen) {
    return <p className="text-muted">Only Office Management can take attendance.</p>;
  }

  const presentAllChecked = visibleRoster.length > 0 && visibleRoster.every((m) => rows[m.id]?.present);
  const weeklyAllChecked =
    visibleRoster.length > 0 && visibleRoster.every((m) => rows[m.id]?.attendedWeeklyClass);
  const editorOpen = mode !== null;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Office Management</p>
          <h1 className="mt-1 text-3xl md:text-4xl">Attendance</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted">
            Take organization-wide attendance, edit past sessions, and message members through the bot.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button type="button" className="btn-primary" onClick={startTakeAttendance} disabled={busy}>
            Take Attendance
          </button>
          <button type="button" className="btn-secondary" onClick={openBroadcast} disabled={busy}>
            Send message via bot
          </button>
          <button type="button" className="btn-secondary" onClick={() => void exportHistory()} disabled={exporting}>
            {exporting ? "Exporting…" : "Export attendance history"}
          </button>
        </div>
      </div>

      {error && <p className="text-sm text-clay">{error}</p>}
      {notice && <p className="text-sm text-teal">{notice}</p>}

      {editorOpen && questionStep && (
        <section className="card space-y-4 p-6">
          <h2 className="text-2xl">Custom questions</h2>
          <p className="text-sm text-muted">
            Optional yes/no questions for this session only. Leave blank and continue if you do not need any.
          </p>
          <label className="block max-w-xs text-sm font-medium">
            Session date
            <input
              type="date"
              className="field mt-1 w-full"
              value={draftDate}
              onChange={(e) => setDraftDate(e.target.value)}
            />
          </label>
          <div className="space-y-2">
            {questionDrafts.map((value, index) => (
              <div key={index} className="flex gap-2">
                <input
                  className="field w-full"
                  value={value}
                  onChange={(e) =>
                    setQuestionDrafts((current) => current.map((item, i) => (i === index ? e.target.value : item)))
                  }
                  placeholder={`Question ${index + 1}`}
                />
                {questionDrafts.length > 1 && (
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={() => setQuestionDrafts((current) => current.filter((_, i) => i !== index))}
                  >
                    Remove
                  </button>
                )}
              </div>
            ))}
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="btn-secondary" onClick={() => setQuestionDrafts((current) => [...current, ""])}>
              Add question
            </button>
            <button type="button" className="btn-primary" onClick={continueFromQuestions}>
              Continue
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setMode(null);
                setQuestionStep(false);
              }}
            >
              Cancel
            </button>
          </div>
        </section>
      )}

      {editorOpen && !questionStep && (
        <section className="card space-y-4 overflow-x-auto p-6">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-2xl">{mode === "edit" ? "Edit attendance" : "Mark attendance"}</h2>
              <p className="text-sm text-muted">{formatSessionDate(draftDate)}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button type="button" className="btn-primary" onClick={() => void save()} disabled={busy}>
                {busy ? "Saving…" : "Save"}
              </button>
              <button
                type="button"
                className="btn-secondary"
                disabled={busy}
                onClick={() => {
                  setMode(null);
                  setEditingId(null);
                }}
              >
                Cancel
              </button>
            </div>
          </div>
          <input
            className="field w-full max-w-md"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search members…"
          />
          <table className="w-full min-w-[40rem] text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs uppercase tracking-[0.12em] text-muted">
                <th className="px-3 py-3">Member</th>
                <th className="px-3 py-3">Group</th>
                <th className="px-3 py-3">
                  <label className="inline-flex items-center gap-2 normal-case tracking-normal">
                    <input
                      type="checkbox"
                      checked={presentAllChecked}
                      onChange={(e) => selectAll("present", e.target.checked)}
                    />
                    Present
                  </label>
                </th>
                <th className="px-3 py-3">
                  <label className="inline-flex items-center gap-2 normal-case tracking-normal">
                    <input
                      type="checkbox"
                      checked={weeklyAllChecked}
                      onChange={(e) => selectAll("attendedWeeklyClass", e.target.checked)}
                    />
                    Weekly class
                  </label>
                </th>
                {questionLabels.map((label, index) => (
                  <th key={questionKeys[index]} className="px-3 py-3 normal-case tracking-normal">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {visibleRoster.map((member) => {
                const row = rows[member.id];
                if (!row) return null;
                return (
                  <tr key={member.id} className="border-b border-line last:border-0">
                    <td className="px-3 py-3 font-semibold">
                      <MemberLink id={member.id} name={member.name} />
                    </td>
                    <td className="px-3 py-3">{member.groupName}</td>
                    <td className="px-3 py-3">
                      <input
                        type="checkbox"
                        checked={row.present}
                        onChange={(e) => patchRow(member.id, { present: e.target.checked })}
                        aria-label={`Present: ${member.name}`}
                      />
                    </td>
                    <td className="px-3 py-3">
                      <input
                        type="checkbox"
                        checked={row.attendedWeeklyClass}
                        onChange={(e) => patchRow(member.id, { attendedWeeklyClass: e.target.checked })}
                        aria-label={`Weekly class: ${member.name}`}
                      />
                    </td>
                    {questionKeys.map((key, index) => (
                      <td key={key} className="px-3 py-3">
                        <input
                          type="checkbox"
                          checked={Boolean(row.answers[key])}
                          onChange={() => toggleAnswer(member.id, key)}
                          aria-label={`${questionLabels[index]}: ${member.name}`}
                        />
                      </td>
                    ))}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </section>
      )}

      <section className="card p-6">
        <h2 className="text-2xl">Past attendance</h2>
        {sessions.length === 0 ? (
          <p className="mt-3 text-muted">No attendance sessions yet.</p>
        ) : (
          <ul className="mt-4 divide-y divide-line">
            {sessions.map((session) => (
              <li key={session.id}>
                <button
                  type="button"
                  className="flex w-full flex-wrap items-center justify-between gap-2 px-1 py-3 text-left hover:text-teal"
                  onClick={() => void openSession(session.id)}
                  disabled={busy}
                >
                  <span className="font-semibold">{formatSessionDate(session.date)}</span>
                  <span className="text-sm text-muted">
                    {session.presentCount}/{session.memberCount} present · {session.createdByName}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {broadcastOpen && (
        <div className="fixed inset-0 z-[70] grid place-items-center bg-ink/40 p-4">
          <div className="card max-h-[90vh] w-full max-w-xl overflow-y-auto p-6">
            <h2 className="text-2xl">Send message via bot</h2>
            <p className="mt-1 text-sm text-muted">
              Sends a Telegram DM to each selected member who has linked the bot. Others are skipped.
            </p>
            <textarea
              className="field mt-4 min-h-32 w-full"
              value={broadcastMessage}
              onChange={(e) => setBroadcastMessage(e.target.value)}
              placeholder="Message"
            />
            <input
              className="field mt-3 w-full"
              value={broadcastSearch}
              onChange={(e) => setBroadcastSearch(e.target.value)}
              placeholder="Search members…"
            />
            <label className="mt-4 flex items-center gap-2 text-sm font-semibold">
              <input
                type="checkbox"
                checked={broadcastVisible.length > 0 && broadcastVisible.every((m) => broadcastSelected.has(m.id))}
                onChange={(e) => {
                  setBroadcastSelected((current) => {
                    const next = new Set(current);
                    for (const member of broadcastVisible) {
                      if (e.target.checked) next.add(member.id);
                      else next.delete(member.id);
                    }
                    return next;
                  });
                }}
              />
              Select all
            </label>
            <ul className="mt-2 max-h-64 space-y-1 overflow-y-auto">
              {broadcastVisible.map((member) => (
                <li key={member.id}>
                  <label className="flex min-h-11 items-center gap-2 rounded-xl px-2 hover:bg-paper">
                    <input
                      type="checkbox"
                      checked={broadcastSelected.has(member.id)}
                      onChange={() => {
                        setBroadcastSelected((current) => {
                          const next = new Set(current);
                          if (next.has(member.id)) next.delete(member.id);
                          else next.add(member.id);
                          return next;
                        });
                      }}
                    />
                    <span className="font-medium">{member.name}</span>
                    <span className="text-xs text-muted">
                      {member.groupName}
                      {!member.hasTelegram ? " · no Telegram" : ""}
                    </span>
                  </label>
                </li>
              ))}
            </ul>
            <div className="mt-4 flex flex-wrap gap-2">
              <button
                type="button"
                className="btn-primary"
                disabled={busy || !broadcastMessage.trim() || broadcastSelected.size === 0}
                onClick={() => void sendBroadcast()}
              >
                {busy ? "Sending…" : "Send"}
              </button>
              <button type="button" className="btn-secondary" disabled={busy} onClick={() => setBroadcastOpen(false)}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function todayIso() {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
}

function formatSessionDate(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  if (!year || !month || !day) return value;
  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}
