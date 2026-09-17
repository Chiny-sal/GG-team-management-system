import type {
  ActivityPage,
  AuthResponse,
  AuthUser,
  Board,
  CommitBoardRequest,
  Dashboard,
  Group,
  Meeting,
  Member,
  MemberProfile,
  MemberWorkSummary,
  NotificationsPage,
  WorkItem,
  WorkItemDetail,
  WorkRegistry,
} from "./types";
import { periodSearchParams, type PeriodSelection } from "./period";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:7223";

/** Hosted backends (e.g. Render) can take this long on a cold start. */
export const FETCH_TIMEOUT_MS = 60_000;
export const COLD_START_HINT_MS = 8_000;
export const COLD_START_HINT = "Waking up the server, this can take up to a minute…";

function getToken() {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("gg.token");
}

function isTimeoutError(error: unknown): boolean {
  if (!(error instanceof Error)) return false;
  return (
    error.name === "TimeoutError" ||
    error.name === "AbortError" ||
    /timed out/i.test(error.message)
  );
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }
  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...init,
      headers,
      signal: init.signal ?? AbortSignal.timeout(FETCH_TIMEOUT_MS),
    });
  } catch (error) {
    if (isTimeoutError(error)) {
      throw new Error(COLD_START_HINT);
    }
    if (error instanceof TypeError) {
      throw new Error("Cannot reach the API at " + API_URL + ". Start the backend and retry.");
    }
    throw error;
  }
  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try {
      const body = await response.json();
      if (body?.message) message = body.message;
    } catch {
      /* ignore */
    }
    throw new Error(message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function periodQuery(selection?: PeriodSelection, weekId?: string) {
  if (!selection && !weekId) return "";
  const params = periodSearchParams(selection ?? { period: "week", year: new Date().getFullYear(), month: new Date().getMonth() + 1 }, weekId);
  return params.toString();
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),
  me: () => request<AuthUser>("/api/auth/me"),
  groups: () => request<Group[]>("/api/groups"),
  currentWeek: () => request<{ weekId: string }>("/api/week/current"),
  dashboard: (selection: PeriodSelection = { period: "week", year: new Date().getFullYear(), month: new Date().getMonth() + 1 }) => {
    const query = periodQuery(selection);
    return request<Dashboard>(`/api/dashboard${query ? `?${query}` : ""}`);
  },
  promoteSuggestion: (id: string) =>
    request(`/api/dashboard/suggestions/${id}/promote`, { method: "POST" }),
  addSuggestion: (text: string) =>
    request(`/api/dashboard/suggestions`, {
      method: "POST",
      body: JSON.stringify({ text }),
    }),
  updateMeetingNotes: (id: string, notes: string | null) =>
    request<Meeting>(`/api/dashboard/meetings/${id}/notes`, {
      method: "PATCH",
      body: JSON.stringify({ notes }),
    }),
  board: (groupId: string, selection: PeriodSelection = { period: "week", year: new Date().getFullYear(), month: new Date().getMonth() + 1 }, weekId?: string) => {
    const query = periodQuery(selection, weekId);
    return request<Board>(`/api/boards/${groupId}${query ? `?${query}` : ""}`);
  },
  registry: (groupId: string) => request<WorkRegistry>(`/api/boards/${groupId}/registry`),
  workItem: (id: string) => request<WorkItemDetail>(`/api/work-items/${id}`),
  addMember: (
    groupId: string,
    payload: { name: string; email: string; password: string; telegramUsername?: string | null },
  ) =>
    request<Member>(`/api/groups/${groupId}/members`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  createGroup: (name: string) =>
    request<Group>("/api/groups", {
      method: "POST",
      body: JSON.stringify({ name }),
    }),
  renameGroup: (groupId: string, name: string) =>
    request<Group>(`/api/groups/${groupId}`, {
      method: "PATCH",
      body: JSON.stringify({ name }),
    }),
  deleteGroup: (groupId: string) => request(`/api/groups/${groupId}`, { method: "DELETE" }),
  commitBoard: (groupId: string, payload: CommitBoardRequest) =>
    request(`/api/boards/${groupId}/commit`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
  createWorkItem: (groupId: string, title: string, deadline?: string | null) =>
    request<WorkItem>(`/api/boards/${groupId}/work-items`, {
      method: "POST",
      body: JSON.stringify({ title, deadline: deadline || null }),
    }),
  updateWorkItem: (
    id: string,
    payload: {
      title?: string;
      description?: string;
      assignedMemberId?: string | null;
      clearAssignment?: boolean;
      status?: string;
      deadline?: string | null;
      clearDeadline?: boolean;
    },
  ) =>
    request<WorkItem>(`/api/work-items/${id}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  deleteWorkItem: (id: string) => request(`/api/work-items/${id}`, { method: "DELETE" }),
  saveBoard: (groupId: string, payload?: CommitBoardRequest) =>
    request(`/api/boards/${groupId}/save`, {
      method: "POST",
      body: JSON.stringify(payload ?? { memberUpdates: [], workItems: [] }),
    }),
  activity: (page = 1, pageSize = 20, search = "") => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (search.trim()) params.set("q", search.trim());
    return request<ActivityPage>(`/api/activity?${params.toString()}`);
  },
  memberProfile: (id: string) => request<MemberProfile>(`/api/members/${id}`),
  updateMemberProfile: (
    id: string,
    payload: { name?: string; email?: string | null; telegramUsername?: string | null },
  ) =>
    request<MemberProfile>(`/api/members/${id}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  changePassword: (id: string, currentPassword: string, newPassword: string) =>
    request(`/api/members/${id}/password`, {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  membersDirectory: () => request<MemberWorkSummary[]>("/api/members"),
  setLeads: (memberIds: string[], extras?: { canViewOtherGroupBoards?: boolean; canAssignWorkToOtherGroups?: boolean }) =>
    request("/api/members/set-leads", {
      method: "POST",
      body: JSON.stringify({
        memberIds,
        ...(extras?.canViewOtherGroupBoards ? { canViewOtherGroupBoards: true } : {}),
        ...(extras?.canAssignWorkToOtherGroups ? { canAssignWorkToOtherGroups: true } : {}),
      }),
    }),
  revokeLead: (memberId: string) =>
    request(`/api/members/${memberId}/revoke-lead`, { method: "POST" }),
  setCrossGroupBoardAccess: (memberId: string, canViewOtherGroupBoards: boolean) =>
    request<MemberProfile>(`/api/members/${memberId}/cross-group-boards`, {
      method: "PATCH",
      body: JSON.stringify({ canViewOtherGroupBoards }),
    }),
  setCrossGroupAssignmentAccess: (memberId: string, canAssignWorkToOtherGroups: boolean) =>
    request<MemberProfile>(`/api/members/${memberId}/cross-group-assignment`, {
      method: "PATCH",
      body: JSON.stringify({ canAssignWorkToOtherGroups }),
    }),
  deleteMember: (memberId: string) =>
    request(`/api/members/${memberId}`, { method: "DELETE" }),
  groupMembers: (groupId: string) => request<Member[]>(`/api/groups/${groupId}/members`),
  async downloadMembersExport() {
    const token = getToken();
    const response = await fetch(`${API_URL}/api/members/export`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    if (!response.ok) throw new Error("Export failed.");
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "members-work-summary.xlsx";
    link.click();
    URL.revokeObjectURL(url);
  },
  notifications: () => request<NotificationsPage>("/api/notifications"),
  markNotificationRead: (id: string) =>
    request(`/api/notifications/${id}/read`, { method: "POST" }),
  exportUrl: (groupId: string, selection: PeriodSelection = { period: "week", year: new Date().getFullYear(), month: new Date().getMonth() + 1 }, weekId?: string) => {
    const query = periodQuery(selection, weekId);
    return `${API_URL}/api/boards/${groupId}/export${query ? `?${query}` : ""}`;
  },
  async downloadExport(groupId: string, selection: PeriodSelection = { period: "week", year: new Date().getFullYear(), month: new Date().getMonth() + 1 }, weekId?: string) {
    const token = getToken();
    const response = await fetch(api.exportUrl(groupId, selection, weekId), {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    if (!response.ok) throw new Error("Export failed.");
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `board-${groupId}.xlsx`;
    link.click();
    URL.revokeObjectURL(url);
  },
};

export { API_URL };

export function dueLabel(deadline: string | null | undefined) {
  return deadline ? `Due ${deadline}` : null;
}
