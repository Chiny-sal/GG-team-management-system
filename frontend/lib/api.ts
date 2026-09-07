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
  NotificationGroup,
  TimePeriod,
  WorkItem,
} from "./types";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:7223";

function getToken() {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("gg.token");
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
      signal: init.signal ?? AbortSignal.timeout(20_000),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw new Error("Request timed out. Is the API running at " + API_URL + "?");
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

function periodQuery(period?: TimePeriod) {
  return period && period !== "week" ? `period=${period}` : "";
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
  dashboard: (period: TimePeriod = "week") => {
    const query = periodQuery(period);
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
  board: (groupId: string, period: TimePeriod = "week", weekId?: string) => {
    const params = new URLSearchParams();
    if (period !== "week") params.set("period", period);
    if (weekId) params.set("weekId", weekId);
    const query = params.toString();
    return request<Board>(`/api/boards/${groupId}${query ? `?${query}` : ""}`);
  },
  addMember: (groupId: string, payload: { name: string; email: string; password: string }) =>
    request<Member>(`/api/groups/${groupId}/members`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),
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
  saveBoard: (groupId: string, payload?: CommitBoardRequest) =>
    request(`/api/boards/${groupId}/save`, {
      method: "POST",
      body: JSON.stringify(payload ?? { memberUpdates: [], workItems: [] }),
    }),
  activity: (page = 1, pageSize = 20) =>
    request<ActivityPage>(`/api/activity?page=${page}&pageSize=${pageSize}`),
  notifications: () => request<NotificationGroup[]>("/api/notifications"),
  markNotificationRead: (id: string) =>
    request(`/api/notifications/${id}/read`, { method: "POST" }),
  exportUrl: (groupId: string, period: TimePeriod = "week", weekId?: string) => {
    const params = new URLSearchParams();
    if (period !== "week") params.set("period", period);
    if (weekId) params.set("weekId", weekId);
    const query = params.toString();
    return `${API_URL}/api/boards/${groupId}/export${query ? `?${query}` : ""}`;
  },
  async downloadExport(groupId: string, period: TimePeriod = "week", weekId?: string) {
    const token = getToken();
    const response = await fetch(api.exportUrl(groupId, period, weekId), {
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
