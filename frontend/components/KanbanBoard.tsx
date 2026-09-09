"use client";

import {
  DndContext,
  DragEndEvent,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { AddMemberDialog } from "@/components/AddMemberDialog";
import { PeriodToggle } from "@/components/PeriodToggle";
import { WorkItemDetailModal } from "@/components/WorkItemDetailModal";
import { api, dueLabel } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { currentPeriodSelection, type PeriodSelection } from "@/lib/period";
import { useLiveReload } from "@/lib/useLiveReload";
import { WORK_ITEM_STATUS_LABELS } from "@/lib/workItem";
import type { Board, CommitBoardRequest, MeetingSummary, Member, WorkItem, WorkItemStatus } from "@/lib/types";

const COLUMNS: WorkItemStatus[] = ["Assigned", "Ongoing", "Done", "NotDone"];
const COLUMN_LABELS = WORK_ITEM_STATUS_LABELS;

export function KanbanBoard({ groupId }: { groupId: string }) {
  const { user, isLead, isOfficeManagement } = useAuth();
  const [board, setBoard] = useState<Board | null>(null);
  const [period, setPeriod] = useState<PeriodSelection>(currentPeriodSelection);
  const [selectedWorkId, setSelectedWorkId] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [deadline, setDeadline] = useState("");
  const [fromMeetingId, setFromMeetingId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dirtyWorkIds, setDirtyWorkIds] = useState<Set<string>>(new Set());
  const [newWorkIds, setNewWorkIds] = useState<Set<string>>(new Set());
  const [dirtyMemberIds, setDirtyMemberIds] = useState<Set<string>>(new Set());
  const [showAddMember, setShowAddMember] = useState(false);
  const [editingGroupName, setEditingGroupName] = useState(false);
  const [groupNameDraft, setGroupNameDraft] = useState("");
  const [renaming, setRenaming] = useState(false);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  const dirty = dirtyWorkIds.size > 0 || newWorkIds.size > 0 || dirtyMemberIds.size > 0;
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;
  const boardRef = useRef(board);
  boardRef.current = board;
  const savingRef = useRef(false);

  const load = useCallback(() => {
    api.board(groupId, period).then((next) => {
      setBoard(next);
      setDirtyWorkIds(new Set());
      setNewWorkIds(new Set());
      setDirtyMemberIds(new Set());
      setFromMeetingId((current) => current || next.meetings?.[0]?.id || "");
    }).catch((e: Error) => setError(e.message));
  }, [groupId, period]);

  useEffect(() => {
    load();
  }, [load]);

  const reloadIfClean = useCallback(() => {
    if (!dirtyRef.current) load();
  }, [load]);
  useLiveReload(reloadIfClean, Boolean(user));

  useEffect(() => {
    function onLeave(event: BeforeUnloadEvent) {
      if (!dirtyRef.current) return;
      event.preventDefault();
      event.returnValue = "";
    }
    window.addEventListener("beforeunload", onLeave);
    window.__ggHasUnsavedChanges = dirty;
    return () => {
      window.removeEventListener("beforeunload", onLeave);
    };
  }, [dirty]);

  useEffect(() => {
    return () => {
      window.__ggHasUnsavedChanges = false;
    };
  }, []);

  const memberById = useMemo(() => {
    const map = new Map<string, Member>();
    board?.members.forEach((member) => map.set(member.id, member));
    return map;
  }, [board]);

  function markWorkDirty(id: string) {
    setDirtyWorkIds((current) => new Set(current).add(id));
  }

  function updateBoardItems(updater: (items: WorkItem[]) => WorkItem[]) {
    setBoard((current) => (current ? { ...current, workItems: updater(current.workItems) } : current));
  }

  function addWork(event: React.FormEvent) {
    event.preventDefault();
    if (!title.trim() || !isLead || !board) return;
    const id = crypto.randomUUID();
    const item: WorkItem = {
      id,
      groupId,
      weekId: board.weekId,
      title: title.trim(),
      description: "",
      assignedMemberId: null,
      assignedMemberName: null,
      status: "NotAssigned",
      deadline: deadline || null,
      createdAt: new Date().toISOString(),
      createdByMemberId: user?.memberId ?? id,
      meetingId: fromMeetingId || null,
    };
    updateBoardItems((items) => [...items, item]);
    setNewWorkIds((current) => new Set(current).add(id));
    setTitle("");
    setDeadline("");
  }

  function buildCommit(): CommitBoardRequest {
    const current = boardRef.current;
    if (!current) return { memberUpdates: [], workItems: [] };
    const workIds = new Set([...dirtyWorkIds, ...newWorkIds]);
    return {
      memberUpdates: current.members
        .filter((member) => dirtyMemberIds.has(member.id))
        .map((member) => ({ id: member.id, name: member.name })),
      workItems: current.workItems
        .filter((item) => workIds.has(item.id))
        .map((item) => ({
          id: item.id,
          isNew: newWorkIds.has(item.id),
          title: item.title,
          description: item.description,
          assignedMemberId: item.assignedMemberId,
          status: item.status,
          deadline: item.deadline,
          meetingId: item.meetingId,
        })),
    };
  }

  async function persistChanges() {
    const current = boardRef.current;
    if (!current || !dirtyRef.current) return;
    await api.commitBoard(groupId, buildCommit());
  }

  async function saveBoard() {
    if (savingRef.current || !boardRef.current || !dirtyRef.current) return;
    savingRef.current = true;
    setBusy(true);
    setError(null);
    try {
      await persistChanges();
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save board.");
    } finally {
      savingRef.current = false;
      setBusy(false);
    }
  }

  async function exportBoard() {
    setError(null);
    try {
      if (dirty) {
        setBusy(true);
        await persistChanges();
        setBusy(false);
      }
      await api.downloadExport(groupId, period, period.period === "week" ? board?.weekId : undefined);
      if (dirty) load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Export failed.");
      setBusy(false);
    }
  }

  function onDragEnd(event: DragEndEvent) {
    if (!board) return;
    const overId = event.over?.id?.toString();
    const itemId = event.active.id.toString();
    if (!overId) return;

    const [memberKey, status] = overId.split(":");
    const item = board.workItems.find((w) => w.id === itemId);
    if (!item) return;

    const assignedMemberId = memberKey === "unassigned" ? null : memberKey;
    const nextStatus = status as WorkItemStatus;

    if (!isLead) {
      if (item.assignedMemberId !== user?.memberId) return;
      if (assignedMemberId !== user?.memberId) return;
      if (nextStatus === "NotAssigned") return;
    }

    if (item.assignedMemberId === assignedMemberId && item.status === (assignedMemberId === null ? "NotAssigned" : nextStatus)) {
      return;
    }

    const assignedMemberName = assignedMemberId ? memberById.get(assignedMemberId)?.name ?? null : null;
    updateBoardItems((items) =>
      items.map((work) =>
        work.id === itemId
          ? {
              ...work,
              assignedMemberId,
              assignedMemberName,
              status: assignedMemberId === null ? "NotAssigned" : nextStatus,
            }
          : work,
      ),
    );
    markWorkDirty(itemId);
  }

  function renameMember(memberId: string, name: string) {
    const trimmed = name.trim();
    if (!trimmed) return;
    setBoard((current) => {
      if (!current) return current;
      const previous = current.members.find((m) => m.id === memberId)?.name;
      if (previous === trimmed) return current;
      return {
        ...current,
        members: current.members.map((member) => (member.id === memberId ? { ...member, name: trimmed } : member)),
        workItems: current.workItems.map((item) =>
          item.assignedMemberId === memberId ? { ...item, assignedMemberName: trimmed } : item,
        ),
      };
    });
    setDirtyMemberIds((current) => new Set(current).add(memberId));
  }

  async function addMember(payload: { name: string; email: string; password: string; telegramUsername?: string }) {
    const member = await api.addMember(groupId, payload);
    setBoard((current) =>
      current
        ? { ...current, members: [...current.members, member].sort((a, b) => a.name.localeCompare(b.name)) }
        : current,
    );
    setShowAddMember(false);
  }

  function changePeriod(next: PeriodSelection) {
    if (
      next.period === period.period &&
      next.year === period.year &&
      next.month === period.month
    ) {
      return;
    }
    if (dirty && !window.confirm("You have unsaved changes. Switch view and discard them?")) return;
    setPeriod(next);
  }

  async function saveGroupName() {
    const name = groupNameDraft.trim();
    if (!name || !board) return;
    setRenaming(true);
    setError(null);
    try {
      const updated = await api.renameGroup(groupId, name);
      setBoard((current) => (current ? { ...current, groupName: updated.name } : current));
      window.dispatchEvent(new Event("gg-groups-changed"));
      setEditingGroupName(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not rename group.");
    } finally {
      setRenaming(false);
    }
  }

  if (!board) {
    return <p className="text-muted">{error ?? "Loading board…"}</p>;
  }

  const unassigned = board.workItems.filter((w) => !w.assignedMemberId || w.status === "NotAssigned");

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Work assigned</p>
          {editingGroupName ? (
            <form
              className="mt-1 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                void saveGroupName();
              }}
            >
              <input
                className="field max-w-md text-2xl font-semibold"
                value={groupNameDraft}
                onChange={(e) => setGroupNameDraft(e.target.value)}
                autoFocus
                maxLength={200}
              />
              <button type="submit" className="btn-primary" disabled={renaming || !groupNameDraft.trim()}>
                {renaming ? "Saving…" : "Save name"}
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setEditingGroupName(false)}
                disabled={renaming}
              >
                Cancel
              </button>
            </form>
          ) : (
            <h1 className="mt-1 text-4xl">{board.groupName}</h1>
          )}
          <p className="mt-1 text-muted">{board.periodLabel ?? `Week of ${board.weekId}`}</p>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <Link href={`/registry/${groupId}`} className="text-sm font-semibold text-teal hover:underline">
              Work registry
            </Link>
            {isOfficeManagement && !editingGroupName && (
              <button
                type="button"
                className="text-sm font-semibold text-teal hover:underline"
                onClick={() => {
                  setGroupNameDraft(board.groupName);
                  setEditingGroupName(true);
                }}
              >
                Edit group name
              </button>
            )}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <PeriodToggle value={period} onChange={changePeriod} />
          {isLead && (
            <button className="btn-primary" onClick={() => setShowAddMember(true)}>
              Add Member
            </button>
          )}
          <button
            type="button"
            disabled={busy}
            onClick={saveBoard}
            title="Save changes to the database"
            className={`btn-primary ${dirty ? "ring-2 ring-clay ring-offset-2 ring-offset-paper" : ""}`}
          >
            {busy ? "Saving…" : "Save"}
          </button>
          <button type="button" onClick={exportBoard} disabled={busy} className="btn-secondary">
            Export
          </button>
        </div>
      </div>

      {dirty && (
        <div className="rounded-2xl bg-teal-soft px-4 py-3 text-sm font-medium text-teal">
          You have unsaved changes. Click Save or Export to write them to the database and activity log.
        </div>
      )}

      {isLead && (
        <form onSubmit={addWork} className="card sticky top-4 z-10 flex flex-wrap gap-2 p-3">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Add work…"
            className="field min-w-56 flex-1"
          />
          <input
            type="date"
            value={deadline}
            onChange={(e) => setDeadline(e.target.value)}
            className="field w-auto"
            aria-label="Due date (optional)"
          />
          <select
            value={fromMeetingId}
            onChange={(e) => setFromMeetingId(e.target.value)}
            className="field w-auto max-w-64"
            aria-label="From meeting (optional)"
          >
            <option value="">No meeting</option>
            {(board.meetings ?? []).map((meeting) => (
              <option key={meeting.id} value={meeting.id}>
                {meetingLabel(meeting)}
              </option>
            ))}
          </select>
          <button type="submit" disabled={busy || !title.trim()} className="btn-primary disabled:opacity-50">
            Add
          </button>
        </form>
      )}

      {error && <p className="text-sm text-clay">{error}</p>}

      <DndContext sensors={sensors} onDragEnd={onDragEnd}>
        <div className="card overflow-x-auto">
          <table className="min-w-[960px] w-full border-collapse">
            <thead>
              <tr className="border-b border-line text-left text-xs font-semibold uppercase tracking-wider text-muted">
                <th className="w-56 p-4">Member</th>
                {COLUMNS.map((column) => (
                  <th key={column} className="p-4">
                    {COLUMN_LABELS[column]}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              <tr className="border-b border-line align-top">
                <td className="bg-paper/80 p-4 font-semibold">Unassigned</td>
                <td colSpan={4} className="p-4">
                  <DropCell id="unassigned:NotAssigned">
                    <div className="flex flex-wrap gap-2">
                      {unassigned.map((item) => (
                        <WorkCard
                          key={item.id}
                          item={item}
                          canDrag={isLead}
                          onOpen={setSelectedWorkId}
                        />
                      ))}
                    </div>
                  </DropCell>
                </td>
              </tr>
              {board.members.map((member) => (
                <tr key={member.id} className="border-b border-line align-top last:border-0">
                  <td className="p-4">
                    <MemberName
                      name={member.name}
                      canEdit={isLead}
                      onCommit={(name) => renameMember(member.id, name)}
                    />
                  </td>
                  {COLUMNS.map((column) => {
                    const items = board.workItems.filter(
                      (w) => w.assignedMemberId === member.id && w.status === column,
                    );
                    const canDrop = isLead || member.id === user?.memberId;
                    return (
                      <td key={column} className="p-3">
                        <DropCell id={`${member.id}:${column}`} disabled={!canDrop}>
                          {items.map((item) => (
                            <WorkCard
                              key={item.id}
                              item={item}
                              canDrag={isLead || item.assignedMemberId === user?.memberId}
                              onOpen={setSelectedWorkId}
                            />
                          ))}
                        </DropCell>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </DndContext>

      {showAddMember && (
        <AddMemberDialog
          groupName={board.groupName}
          onClose={() => setShowAddMember(false)}
          onSubmit={addMember}
        />
      )}

      {selectedWorkId && (
        <WorkItemDetailModal
          workItemId={selectedWorkId}
          fallback={board.workItems.find((item) => item.id === selectedWorkId)}
          groupName={board.groupName}
          onClose={() => setSelectedWorkId(null)}
        />
      )}
    </div>
  );
}

function meetingLabel(meeting: MeetingSummary) {
  const topic = meeting.topicText?.trim();
  return topic ? `${meeting.scheduledDate} · ${topic}` : meeting.scheduledDate;
}

function MemberName({
  name,
  canEdit,
  onCommit,
}: {
  name: string;
  canEdit: boolean;
  onCommit: (name: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(name);

  useEffect(() => {
    if (!editing) setValue(name);
  }, [name, editing]);

  if (!canEdit) return <p className="font-semibold">{name}</p>;

  if (!editing) {
    return (
      <button
        type="button"
        className="rounded-lg px-1 py-0.5 text-left font-semibold hover:bg-teal-soft"
        onClick={() => setEditing(true)}
        title="Click to rename"
      >
        {name}
      </button>
    );
  }

  return (
    <input
      autoFocus
      className="field py-1"
      value={value}
      onChange={(e) => setValue(e.target.value)}
      onBlur={() => {
        setEditing(false);
        if (value.trim() && value.trim() !== name) onCommit(value);
        else setValue(name);
      }}
      onKeyDown={(e) => {
        if (e.key === "Enter") (e.target as HTMLInputElement).blur();
        if (e.key === "Escape") {
          setValue(name);
          setEditing(false);
        }
      }}
    />
  );
}

function DropCell({
  id,
  disabled,
  children,
}: {
  id: string;
  disabled?: boolean;
  children: React.ReactNode;
}) {
  const { setNodeRef, isOver } = useDroppable({ id, disabled });
  return (
    <div
      ref={setNodeRef}
      className={`min-h-24 rounded-xl border border-dashed p-2 ${
        isOver ? "border-teal bg-teal-soft" : "border-line/70"
      } ${disabled ? "opacity-70" : ""}`}
    >
      {children}
    </div>
  );
}

function WorkCard({
  item,
  canDrag,
  onOpen,
}: {
  item: WorkItem;
  canDrag: boolean;
  onOpen: (id: string) => void;
}) {
  const dragged = useRef(false);
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: item.id,
    disabled: !canDrag,
  });
  const due = dueLabel(item.deadline);

  useEffect(() => {
    if (isDragging) dragged.current = true;
  }, [isDragging]);

  return (
    <article
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform) }}
      className={`mb-2 rounded-xl bg-paper p-3 shadow-[0_6px_16px_rgba(28,25,23,0.05)] ${
        isDragging ? "opacity-60" : ""
      } ${canDrag ? "cursor-grab" : "cursor-pointer"}`}
      onClick={() => {
        if (dragged.current) {
          dragged.current = false;
          return;
        }
        onOpen(item.id);
      }}
      {...listeners}
      {...attributes}
    >
      <h3 className="font-semibold">{item.title}</h3>
      <p className="mt-1 text-xs text-muted">
        {[due, COLUMN_LABELS[item.status]].filter(Boolean).join(" · ")}
      </p>
    </article>
  );
}
