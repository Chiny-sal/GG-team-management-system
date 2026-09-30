export function ShowMoreButton({
  remaining,
  onClick,
  busy = false,
}: {
  remaining: number;
  onClick: () => void;
  busy?: boolean;
}) {
  if (remaining <= 0) return null;

  return (
    <button type="button" className="btn-secondary mt-4" onClick={onClick} disabled={busy}>
      {busy ? "Loading…" : `Show more · ${remaining} remaining`}
    </button>
  );
}
