"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { formatDateTime, WORK_ITEM_STATUS_LABELS } from "@/lib/workItem";
import { MemberLink } from "@/components/MemberLink";
import type { WorkItem, WorkItemDetail } from "@/lib/types";

export function WorkItemDetailModal({
  workItemId,
  fallback,
  groupName,
  onClose,
}: {
  workItemId: string;
  fallback?: WorkItem | null;
  groupName?: string | null;
  onClose: () => void;
}) {
  const [detail, setDetail] = useState<WorkItemDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .workItem(workItemId)
      .then((next) => {
        if (!cancelled) setDetail(next);
      })
      .catch((e: Error) => {
        if (cancelled) return;
        if (fallback) {
          setDetail(fromFallback(fallback, groupName));
          setError(null);
        } else {
          setError(e.message);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [workItemId, fallback, groupName]);

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-ink/30 px-4" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="work-item-detail-title"
        className="card max-h-[90vh] w-full max-w-lg overflow-y-auto p-6"
        onClick={(event) => event.stopPropagation()}
      >
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-teal">Work item</p>
        {loading && !detail ? (
          <p className="mt-3 text-sm text-muted">Loading details…</p>
        ) : error ? (
          <p className="mt-3 text-sm text-clay">{error}</p>
        ) : detail ? (
          <>
            <h2 id="work-item-detail-title" className="mt-1 text-2xl">
              {detail.title}
            </h2>
            <dl className="mt-5 space-y-3 text-sm">
              <DetailField label="Description">
                {detail.description?.trim() ? (
                  <span className="whitespace-pre-wrap">{detail.description}</span>
                ) : (
                  <span className="text-muted">No description</span>
                )}
              </DetailField>
              <DetailField label="Status">{WORK_ITEM_STATUS_LABELS[detail.status] ?? detail.status}</DetailField>
              <DetailField label="Assigned member">
                {detail.assignedMemberId ? (
                  <MemberLink id={detail.assignedMemberId} name={detail.assignedMemberName} />
                ) : (
                  "Unassigned"
                )}
              </DetailField>
              <DetailField label="Deadline">{detail.deadline ?? "No deadline set"}</DetailField>
              <DetailField label="Group">{detail.groupName || groupName || "Unknown group"}</DetailField>
              <DetailField label="Created">{formatDateTime(detail.createdAt) ?? detail.createdAt}</DetailField>
              <DetailField label="Created by">
                {detail.createdByMemberId ? (
                  <MemberLink id={detail.createdByMemberId} name={detail.createdByMemberName} />
                ) : (
                  (detail.createdByMemberName ?? "Unknown")
                )}
              </DetailField>
              <DetailField label="Meeting">
                {detail.meetingId
                  ? [detail.meetingScheduledDate, detail.meetingTopicText].filter(Boolean).join(" · ") ||
                    "Linked meeting"
                  : "Not linked to a meeting"}
              </DetailField>
            </dl>
          </>
        ) : null}
        <div className="mt-6 flex justify-end">
          <button type="button" className="btn-secondary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

function DetailField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{label}</dt>
      <dd className="mt-1">{children}</dd>
    </div>
  );
}

function fromFallback(item: WorkItem, groupName?: string | null): WorkItemDetail {
  return {
    id: item.id,
    groupId: item.groupId,
    groupName: groupName ?? "",
    weekId: item.weekId,
    title: item.title,
    description: item.description,
    assignedMemberId: item.assignedMemberId,
    assignedMemberName: item.assignedMemberName,
    status: item.status,
    deadline: item.deadline,
    createdAt: item.createdAt,
    createdByMemberId: item.createdByMemberId,
    createdByMemberName: null,
    meetingId: item.meetingId,
    meetingTopicText: null,
    meetingScheduledDate: null,
  };
}
