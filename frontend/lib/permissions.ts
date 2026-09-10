import type { AuthUser } from "./types";

export function canViewAllGroups(user: AuthUser | null | undefined): boolean {
  return user?.isOfficeManagement === true || user?.canViewOtherGroupBoards === true;
}

export function canAssignWorkToOtherGroups(user: AuthUser | null | undefined): boolean {
  return user?.isOfficeManagement === true || user?.canAssignWorkToOtherGroups === true;
}

export function canManageGroup(user: AuthUser | null | undefined, groupId: string): boolean {
  if (!user) return false;
  if (user.isOfficeManagement) return true;
  return user.role === "Lead" && user.groupId === groupId;
}

export function canManageWorkOnGroup(user: AuthUser | null | undefined, groupId: string): boolean {
  if (canManageGroup(user, groupId)) return true;
  return Boolean(user && canAssignWorkToOtherGroups(user) && user.groupId !== groupId);
}

export function canAddWork(user: AuthUser | null | undefined, groupId: string): boolean {
  return canManageWorkOnGroup(user, groupId) || canAssignWorkToOtherGroups(user);
}

export function canAddMember(user: AuthUser | null | undefined, groupId: string): boolean {
  return canManageGroup(user, groupId);
}
