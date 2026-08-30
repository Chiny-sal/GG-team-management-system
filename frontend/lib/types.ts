export type MemberRole = "Lead" | "Member";
export type WorkItemStatus = "NotAssigned" | "Assigned" | "Ongoing" | "Done" | "NotDone";
export type NotificationType = "MemberNoAssignmentTwoWeeks" | "WorkNotDoneTwoWeeks";
export type ChangeType = "Created" | "Updated" | "Deleted";

export type AuthUser = {
  token?: string;
  memberId: string;
  groupId: string;
  name: string;
  role: MemberRole;
  email: string;
};

export type AuthResponse = AuthUser & { token: string };

export type Group = {
  id: string;
  name: string;
  isOfficeManagementTeam: boolean;
};

export type Member = {
  id: string;
  name: string;
  groupId: string;
  role: MemberRole;
  telegramUserId: string | null;
};

export type WorkItem = {
  id: string;
  groupId: string;
  weekId: string;
  title: string;
  description: string;
  assignedMemberId: string | null;
  assignedMemberName: string | null;
  status: WorkItemStatus;
  deadline: string;
  createdAt: string;
  createdByMemberId: string;
};

export type Board = {
  groupId: string;
  groupName: string;
  weekId: string;
  isLocked: boolean;
  savedAt: string | null;
  members: Member[];
  workItems: WorkItem[];
};

export type TopicSuggestion = {
  id: string;
  submittedByTelegramUserId: string;
  submittedByName: string;
  text: string;
  submittedAt: string;
  promotedToMeetingId: string | null;
};

export type Meeting = {
  id: string;
  scheduledDate: string;
  topicText: string | null;
};

export type GroupSummary = {
  groupId: string;
  groupName: string;
  assignedCount: number;
  doneCount: number;
  notDoneCount: number;
  ongoingCount: number;
  unassignedCount: number;
};

export type Dashboard = {
  currentMeeting: Meeting | null;
  pendingSuggestions: TopicSuggestion[];
  groupSummaries: GroupSummary[];
  doneThisWeek: WorkItem[];
  notDoneThisWeek: WorkItem[];
  assignedThisWeek: WorkItem[];
};

export type ActivityLog = {
  id: string;
  entityType: string;
  entityId: string;
  changeType: ChangeType;
  summary: string;
  changedByMemberId: string | null;
  changedByName: string | null;
  occurredAt: string;
};

export type ActivityPage = {
  items: ActivityLog[];
  total: number;
  page: number;
  pageSize: number;
};

export type Notification = {
  id: string;
  type: NotificationType;
  memberId: string | null;
  memberName: string | null;
  workItemId: string | null;
  workItemTitle: string | null;
  createdAt: string;
  isRead: boolean;
};

export type NotificationGroup = {
  type: NotificationType;
  items: Notification[];
};
