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
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import type { Board, WorkItem, WorkItemStatus } from "@/lib/types";

const COLUMNS: WorkItemStatus[] = ["Assigned", "Ongoing", "Done", "NotDone"];

export function KanbanBoard({ groupId }: { groupId: string }) {
  const { user, isLead } = useAuth();
  const [board, setBoard] = useState<Board | null>(null);
  const [title, setTitle] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  const load = useCallback(() => {
    api.board(groupId).then(setBoard).catch((e: Error) => setError(e.message));
  }, [groupId]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  async function addWork(event: React.FormEvent) {
    event.preventDefault();
    if (!title.trim() || !isLead) return;
    setBusy(true);
    setError(null);
    try {
      await api.createWorkItem(groupId, title.trim());
      setTitle("");
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not add work.");
    } finally {
      setBusy(false);
    }
  }

  async function saveBoard() {
    setBusy(true);
    setError(null);
    try {
      await api.saveBoard(groupId);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save board.");
    } finally {
      setBusy(false);
    }
  }

  async function exportBoard() {
    setError(null);
    try {
      await api.downloadExport(groupId, board?.weekId);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Export failed.");
    }
  }

  async function onDragEnd(event: DragEndEvent) {
    if (!board || board.isLocked) return;
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

    try {
      await api.updateWorkItem(itemId, {
        assignedMemberId: assignedMemberId ?? undefined,
        clearAssignment: assignedMemberId === null,
        status: assignedMemberId === null ? "NotAssigned" : nextStatus,
      });
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Move failed.");
    }
  }

  if (!board) {
    return <p className="text-muted">{error ?? "Loading board…"}</p>;
  }

  const unassigned = board.workItems.filter((w) => !w.assignedMemberId || w.status === "NotAssigned");

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.2em] text-teal">Work assigned</p>
          <h1 className="serif text-4xl">{board.groupName}</h1>
          <p className="text-muted">
            Week of {board.weekId}
            {board.isLocked ? ` · locked ${board.savedAt ? new Date(board.savedAt).toLocaleString() : ""}` : ""}
          </p>
        </div>
        <div className="flex gap-2">
          {isLead && (
            <button
              disabled={busy || board.isLocked}
              onClick={saveBoard}
              className="rounded-full bg-ink px-4 py-2 text-sm text-white disabled:opacity-50"
            >
              Save
            </button>
          )}
          <button onClick={exportBoard} className="rounded-full border border-line bg-card px-4 py-2 text-sm">
            Export
          </button>
        </div>
      </div>

      {isLead && !board.isLocked && (
        <form onSubmit={addWork} className="sticky top-0 z-10 flex gap-2 rounded-2xl border border-line bg-card p-3 shadow-sm">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Add work…"
            className="flex-1 rounded-xl border border-line bg-paper px-3 py-2 outline-none focus:border-teal"
          />
          <button disabled={busy || !title.trim()} className="rounded-xl bg-teal px-4 py-2 text-white disabled:opacity-50">
            Add
          </button>
        </form>
      )}

      {error && <p className="text-sm text-clay">{error}</p>}

      <DndContext sensors={sensors} onDragEnd={onDragEnd}>
        <div className="overflow-x-auto rounded-2xl border border-line bg-card">
          <table className="min-w-[960px] w-full border-collapse">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase tracking-wider text-muted">
                <th className="w-48 p-3">Member</th>
                {COLUMNS.map((column) => (
                  <th key={column} className="p-3">
                    {column}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              <tr className="border-b border-line align-top">
                <td className="bg-paper/70 p-3 font-medium">Unassigned</td>
                <td colSpan={4} className="p-3">
                  <DropCell id="unassigned:NotAssigned" disabled={board.isLocked}>
                    <div className="flex flex-wrap gap-2">
                      {unassigned.map((item) => (
                        <WorkCard key={item.id} item={item} locked={board.isLocked} canDrag={isLead && !board.isLocked} />
                      ))}
                    </div>
                  </DropCell>
                </td>
              </tr>
              {board.members.map((member) => (
                <tr key={member.id} className="border-b border-line align-top last:border-0">
                  <td className="p-3 font-medium">{member.name}</td>
                  {COLUMNS.map((column) => {
                    const items = board.workItems.filter(
                      (w) => w.assignedMemberId === member.id && w.status === column,
                    );
                    const canDrop =
                      !board.isLocked && (isLead || member.id === user?.memberId);
                    return (
                      <td key={column} className="p-2">
                        <DropCell id={`${member.id}:${column}`} disabled={!canDrop}>
                          {items.map((item) => (
                            <WorkCard
                              key={item.id}
                              item={item}
                              locked={board.isLocked}
                              canDrag={
                                !board.isLocked &&
                                (isLead || item.assignedMemberId === user?.memberId)
                              }
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
    </div>
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
  locked,
  canDrag,
}: {
  item: WorkItem;
  locked: boolean;
  canDrag: boolean;
}) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: item.id,
    disabled: !canDrag,
  });

  return (
    <article
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform) }}
      className={`mb-2 rounded-xl border border-line bg-paper p-3 shadow-sm ${
        isDragging ? "opacity-60" : ""
      } ${canDrag ? "cursor-grab" : "cursor-default"}`}
      {...listeners}
      {...attributes}
    >
      <h3 className="font-medium">{item.title}</h3>
      <p className="mt-1 text-xs text-muted">
        Due {item.deadline} · {item.status}
        {locked ? " · locked" : ""}
      </p>
    </article>
  );
}
