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

  return (
    <section className="card p-6">
      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-clay">Pending approvals</p>
      <h2 className="mt-2 text-2xl">Deletion requests</h2>
      <p className="mt-1 text-sm text-muted">
        A different Office Management member must approve a request before the group or member is actually removed.
      </p>
      {requests.length === 0 ? (
        <p className="mt-4 text-sm text-muted">No pending deletion requests.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {requests.map((request) => {
            const ownRequest = request.requestedByMemberId === user.memberId;
            const busy = busyId === request.id;
            const kind = request.targetType === "Member" ? "Member" : "Group";
            return (
              <li key={request.id} className="rounded-2xl bg-paper px-4 py-3">
                <p className="font-semibold">
                  {kind}: {request.targetName}
                </p>
                <p className="mt-1 text-xs text-muted">
                  Requested by {request.requestedByName ?? "Office Management"} ·{" "}
                  {new Date(request.requestedAt).toLocaleString()}
                  {ownRequest ? " · waiting for another Office Management member" : ""}
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
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
                    Cancel request
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
