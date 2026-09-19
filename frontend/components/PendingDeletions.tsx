"use client";

import { useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { DeletionRequest } from "@/lib/types";

export function PendingDeletions({
  requests,
  onChanged,
  onError,
}: {
  requests: DeletionRequest[];
  onChanged: () => void;
  onError: (message: string | null) => void;
}) {
  const { user } = useAuth();
  const [busyId, setBusyId] = useState<string | null>(null);

  if (!user?.isOfficeManagement) return null;

  async function act(id: string, action: "approve" | "cancel") {
    setBusyId(id);
    onError(null);
    try {
      if (action === "approve") await api.approveDeletionRequest(id);
      else await api.cancelDeletionRequest(id);
      onChanged();
    } catch (e) {
      onError(e instanceof Error ? e.message : "Could not update the deletion request.");
    } finally {
      setBusyId(null);
    }
  }

  const count = requests.length;

  return (
    <details className="rounded-2xl border border-line bg-card px-4 py-3" open={count > 0}>
      <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3">
        <span>
          <span className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">Pending approvals</span>
          <span className="mt-0.5 block text-sm font-medium">
            {count === 0
              ? "No deletion requests"
              : `${count} deletion request${count === 1 ? "" : "s"} waiting`}
          </span>
        </span>
        <span className="text-xs text-muted">{count > 0 ? "Hide" : "Details"}</span>
      </summary>
      <p className="mt-2 text-xs text-muted">
        A different Office Management member must approve a request before the group or member is actually removed.
      </p>
      {count > 0 && (
        <ul className="mt-3 space-y-2">
          {requests.map((request) => {
            const ownRequest = request.requestedByMemberId === user.memberId;
            const busy = busyId === request.id;
            const kind = request.targetType === "Member" ? "Member" : "Group";
            return (
              <li key={request.id} className="rounded-xl bg-paper px-3 py-2">
                <p className="text-sm font-semibold">
                  {kind}: {request.targetName}
                </p>
                <p className="mt-0.5 text-xs text-muted">
                  {request.requestedByName ?? "Office Management"} · {new Date(request.requestedAt).toLocaleString()}
                  {ownRequest ? " · waiting for another Office Management member" : ""}
                </p>
                <div className="mt-2 flex flex-wrap gap-2">
                  <button
                    type="button"
                    className="btn-primary disabled:opacity-50"
                    disabled={busy || ownRequest}
                    title={ownRequest ? "You cannot approve your own request" : "Approve deletion"}
                    onClick={() => void act(request.id, "approve")}
                  >
                    {busy ? "Working…" : "Approve"}
                  </button>
                  <button
                    type="button"
                    className="btn-secondary disabled:opacity-50"
                    disabled={busy}
                    onClick={() => void act(request.id, "cancel")}
                  >
                    Cancel
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </details>
  );
}
