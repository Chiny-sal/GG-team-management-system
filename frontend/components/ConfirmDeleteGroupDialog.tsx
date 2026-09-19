"use client";

export function ConfirmDeleteGroupDialog({
  groupName,
  busy = false,
  error,
  onCancel,
  onConfirm,
}: {
  groupName: string;
  busy?: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="dialog-overlay">
      <div role="dialog" aria-modal="true" aria-labelledby="delete-group-title" className="dialog-panel card max-w-md p-5 sm:p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">Delete group</p>
        <h2 id="delete-group-title" className="mt-1 text-2xl">
          Delete {groupName}?
        </h2>
        <p className="mt-2 text-sm text-muted">
          This does not delete immediately. It creates a pending request that a different Office Management member must
          approve on the Dashboard. After approval, the group&apos;s work items and members (including their logins) are
          removed. Members are not moved to another group. You can cancel the request from Pending approvals instead.
        </p>
        {error && <p className="mt-3 text-sm text-clay">{error}</p>}
        <div className="dialog-actions mt-6">
          <button type="button" className="btn-secondary" onClick={onCancel} disabled={busy}>
            Cancel
          </button>
          <button type="button" className="btn-danger" onClick={onConfirm} disabled={busy}>
            {busy ? "Requesting…" : "Request deletion"}
          </button>
        </div>
      </div>
    </div>
  );
}
