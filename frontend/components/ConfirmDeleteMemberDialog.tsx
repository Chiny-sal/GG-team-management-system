"use client";

export function ConfirmDeleteMemberDialog({
  memberName,
  busy = false,
  error,
  onCancel,
  onConfirm,
}: {
  memberName: string;
  busy?: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div className="fixed inset-0 z-40 grid place-items-center bg-ink/30 px-4">
      <div role="dialog" aria-modal="true" aria-labelledby="delete-member-title" className="card w-full max-w-md p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">Delete member</p>
        <h2 id="delete-member-title" className="mt-1 text-2xl">
          Remove {memberName}?
        </h2>
        <p className="mt-2 text-sm text-muted">
          Are you sure you want to delete {memberName}? All active tasks assigned to them will be moved to Unassigned.
          Their login will be removed. Boards and work items they created will stay in the system.
        </p>
        {error && <p className="mt-3 text-sm text-clay">{error}</p>}
        <div className="mt-6 flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onCancel} disabled={busy}>
            Cancel
          </button>
          <button type="button" className="btn-danger" onClick={onConfirm} disabled={busy}>
            {busy ? "Deleting…" : "Delete member"}
          </button>
        </div>
      </div>
    </div>
  );
}

export function DeleteMemberIconButton({
  memberName,
  disabled,
  onClick,
}: {
  memberName: string;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className="inline-flex h-8 w-8 items-center justify-center rounded-full text-clay hover:bg-teal-soft disabled:opacity-40"
      aria-label={`Delete ${memberName}`}
      title={`Delete ${memberName}`}
      disabled={disabled}
      onClick={(event) => {
        event.preventDefault();
        event.stopPropagation();
        onClick();
      }}
    >
      <TrashIcon />
    </button>
  );
}

function TrashIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" aria-hidden="true" className="h-4 w-4">
      <path
        d="M7 4.5h6M4.5 6.5h11M8 6.5V15a1 1 0 0 0 1 1h2a1 1 0 0 0 1-1V6.5M7.5 9v5M12.5 9v5"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
