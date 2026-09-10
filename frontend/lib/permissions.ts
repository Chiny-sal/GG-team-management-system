import type { AuthUser } from "./types";

export function canViewAllGroups(user: AuthUser | null | undefined): boolean {
  return user?.isOfficeManagement === true || user?.canViewOtherGroupBoards === true;
}

export function canManageGroup(user: AuthUser | null | undefined, groupId: string): boolean {
  if (!user) return false;
  if (user.isOfficeManagement) return true;
  return user.role === "Lead" && user.groupId === groupId;
}

export function canAddWork(user: AuthUser | null | undefined, groupId: string): boolean {
  return canManageGroup(user, groupId);
}

export function canAddMember(user: AuthUser | null | undefined, groupId: string): boolean {
  return canManageGroup(user, groupId);
}
