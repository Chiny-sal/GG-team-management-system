import type { WorkItemStatus } from "./types";

export const WORK_ITEM_STATUS_LABELS: Record<WorkItemStatus, string> = {
  NotAssigned: "Unassigned",
  Assigned: "Assigned",
  Ongoing: "Ongoing",
  Done: "Done",
  NotDone: "Not Done",
};

export function formatDateTime(value: string | null | undefined) {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}
