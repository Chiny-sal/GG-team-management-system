"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { WorkItemDetailModal } from "@/components/WorkItemDetailModal";
import { MemberLink } from "@/components/MemberLink";
import { api, dueLabel } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useLiveReload } from "@/lib/useLiveReload";
import { WORK_ITEM_STATUS_LABELS } from "@/lib/workItem";
import type { WorkItem, WorkRegistry } from "@/lib/types";

export default function RegistryPage() {
  const params = useParams<{ groupId: string }>();
  const groupId = params.groupId;
  const { user } = useAuth();
  const [data, setData] = useState<WorkRegistry | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [selectedWorkId, setSelectedWorkId] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!user || !groupId) return;
    api.registry(groupId).then(setData).catch((e: Error) => setError(e.message));
  }, [user, groupId]);

  useEffect(() => {
    load();
  }, [load]);
  useLiveReload(load, Boolean(user));

  const filtered = useMemo(() => {
    if (!data) return [];
    const needle = query.trim().toLowerCase();
    if (!needle) return data.workItems;
    return data.workItems.filter((item) => {
      const haystack = `${item.title} ${item.description}`.toLowerCase();
      return haystack.includes(needle);
    });
  }, [data, query]);

  const selectedWork = data?.workItems.find((item) => item.id === selectedWorkId);

  if (!data) return <p className="text-muted">{error ?? "Loading work registry…"}</p>;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">Work registry</p>
          <h1 className="mt-1 text-4xl">{data.groupName}</h1>
          <p className="mt-1 text-sm text-muted">
            Every work item registered for this group, not limited to the current week, month, or year.
          </p>
          <Link href={`/board/${groupId}`} className="mt-2 inline-block text-sm font-semibold text-teal hover:underline">
            Open board
          </Link>
        </div>
      </div>

      {error && <p className="text-sm text-clay">{error}</p>}

      <input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search by title or description…"
        className="field max-w-xl"
        aria-label="Search work items"
      />

      <section className="card p-5">
        {filtered.length === 0 ? (
          <p className="text-sm text-muted">
            {data.workItems.length === 0 ? "No work items have been registered yet." : "No work items match that search."}
          </p>
        ) : (
          <ul className="divide-y divide-line">
            {filtered.map((item) => (
              <RegistryRow key={item.id} item={item} onOpen={setSelectedWorkId} />
            ))}
          </ul>
        )}
      </section>

      {selectedWorkId && (
        <WorkItemDetailModal
          workItemId={selectedWorkId}
          fallback={selectedWork}
          groupName={data.groupName}
          onClose={() => setSelectedWorkId(null)}
        />
      )}
    </div>
  );
}

function RegistryRow({ item, onOpen }: { item: WorkItem; onOpen: (id: string) => void }) {
  const due = dueLabel(item.deadline);
  return (
    <li>
      <button
        type="button"
        onClick={() => onOpen(item.id)}
        className="flex w-full flex-col gap-1 px-2 py-3 text-left transition hover:bg-paper sm:flex-row sm:items-baseline sm:justify-between"
      >
        <div>
          <p className="font-semibold">{item.title}</p>
          {item.description.trim() ? (
            <p className="mt-1 line-clamp-2 text-sm text-muted">{item.description}</p>
          ) : null}
        </div>
        <p className="shrink-0 text-sm text-muted">
          {item.assignedMemberId ? (
            <MemberLink id={item.assignedMemberId} name={item.assignedMemberName} />
          ) : (
            "Unassigned"
          )}
          {` · ${WORK_ITEM_STATUS_LABELS[item.status]}`}
          {due ? ` · ${due}` : ""}
        </p>
      </button>
    </li>
  );
}
