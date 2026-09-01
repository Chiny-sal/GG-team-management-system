import type {
  ActivityPage,
  AuthResponse,
  AuthUser,
  Board,
  Dashboard,
  Group,
  NotificationGroup,
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

  const response = await fetch(`${API_URL}${path}`, { ...init, headers });
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

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    }),
  me: () => request<AuthUser>("/api/auth/me"),
  groups: () => request<Group[]>("/api/groups"),
  currentWeek: () => request<{ weekId: string }>("/api/week/current"),
  dashboard: () => request<Dashboard>("/api/dashboard"),
  promoteSuggestion: (id: string) =>
    request(`/api/dashboard/suggestions/${id}/promote`, { method: "POST" }),
  board: (groupId: string, weekId?: string) =>
    request<Board>(`/api/boards/${groupId}${weekId ? `?weekId=${weekId}` : ""}`),
  createWorkItem: (groupId: string, title: string, deadline?: string) =>
    request<WorkItem>(`/api/boards/${groupId}/work-items`, {
      method: "POST",
      body: JSON.stringify({ title, deadline }),
    }),
  updateWorkItem: (
    id: string,
    payload: {
      title?: string;
      description?: string;
      assignedMemberId?: string | null;
      clearAssignment?: boolean;
      status?: string;
      deadline?: string;
    },
  ) =>
    request<WorkItem>(`/api/work-items/${id}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    }),
  saveBoard: (groupId: string) =>
    request(`/api/boards/${groupId}/save`, { method: "POST" }),
  activity: (page = 1, pageSize = 20) =>
    request<ActivityPage>(`/api/activity?page=${page}&pageSize=${pageSize}`),
  notifications: () => request<NotificationGroup[]>("/api/notifications"),
  markNotificationRead: (id: string) =>
    request(`/api/notifications/${id}/read`, { method: "POST" }),
  exportUrl: (groupId: string, weekId?: string) =>
    `${API_URL}/api/boards/${groupId}/export${weekId ? `?weekId=${weekId}` : ""}`,
  async downloadExport(groupId: string, weekId?: string) {
    const token = getToken();
    const response = await fetch(api.exportUrl(groupId, weekId), {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    if (!response.ok) throw new Error("Export failed.");
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `board-${groupId}${weekId ? `-${weekId}` : ""}.xlsx`;
    link.click();
    URL.revokeObjectURL(url);
  },
};

export { API_URL };
