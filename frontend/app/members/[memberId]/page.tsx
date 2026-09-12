"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ConfirmDeleteMemberDialog } from "@/components/ConfirmDeleteMemberDialog";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { MemberProfile } from "@/lib/types";

export default function MemberProfilePage() {
  const { memberId } = useParams<{ memberId: string }>();
  const router = useRouter();
  const { user, isLead, isOfficeManagement } = useAuth();
  const [profile, setProfile] = useState<MemberProfile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [telegramUsername, setTelegramUsername] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [saved, setSaved] = useState<string | null>(null);
  const [allowOtherBoards, setAllowOtherBoards] = useState(false);
  const [allowAssignOtherGroups, setAllowAssignOtherGroups] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const load = useCallback(() => {
    if (!user || !memberId) return;
    api
      .memberProfile(memberId)
      .then((next) => {
        setProfile(next);
        setName(next.name);
        setEmail(next.email ?? "");
        setTelegramUsername(next.telegramUsername ?? "");
      })
      .catch((e: Error) => setError(e.message));
  }, [user, memberId]);

  useEffect(() => {
    load();
  }, [load]);

  async function saveProfile(event: React.FormEvent) {
    event.preventDefault();
    if (!profile) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      const next = await api.updateMemberProfile(profile.id, {
        name: name.trim(),
        email: email.trim() || null,
        telegramUsername: telegramUsername.trim() || "",
      });
      setProfile(next);
      setSaved("Profile saved.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save profile.");
    } finally {
      setBusy(false);
    }
  }

  async function savePassword(event: React.FormEvent) {
    event.preventDefault();
    if (!profile) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api.changePassword(profile.id, currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setSaved("Password updated.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not change password.");
    } finally {
      setBusy(false);
    }
  }

  async function setAsLead() {
    if (!profile) return;
    if (!window.confirm(`Set ${profile.name} as Team Lead?`)) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api.setLeads([profile.id], {
        ...(allowOtherBoards ? { canViewOtherGroupBoards: true } : {}),
        ...(allowAssignOtherGroups ? { canAssignWorkToOtherGroups: true } : {}),
      });
      load();
      const extras = [
        allowOtherBoards ? "can view other groups' boards" : null,
        allowAssignOtherGroups ? "can assign work to other groups" : null,
      ].filter(Boolean);
      setSaved(
        extras.length > 0
          ? `${profile.name} is now a Team Lead and ${extras.join(" and ")}.`
          : `${profile.name} is now a Team Lead.`,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not set Team Lead.");
    } finally {
      setBusy(false);
    }
  }

  async function revokeLead() {
    if (!profile) return;
    if (!window.confirm(`Remove ${profile.name} as Team Lead? This does not change their access to other groups' boards.`))
      return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api.revokeLead(profile.id);
      load();
      setSaved(`${profile.name} is no longer a Team Lead.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not remove Team Lead.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleOtherAssignmentAccess() {
    if (!profile) return;
    const next = !profile.canAssignWorkToOtherGroups;
    const action = next
      ? `Allow ${profile.name} to assign work to other groups?`
      : `Revoke ${profile.name}'s permission to assign work to other groups? This does not change Team Lead status or board view access.`;
    if (!window.confirm(action)) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      const updated = await api.setCrossGroupAssignmentAccess(profile.id, next);
      setProfile(updated);
      setSaved(
        next
          ? `${profile.name} can now assign work to other groups.`
          : `${profile.name} can no longer assign work to other groups.`,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not update assignment permission.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleOtherBoardAccess() {
    if (!profile) return;
    const next = !profile.canViewOtherGroupBoards;
    const action = next
      ? `Allow ${profile.name} to view other groups' boards?`
      : `Revoke ${profile.name}'s view access to other groups' boards? This does not change Team Lead status.`;
    if (!window.confirm(action)) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      const updated = await api.setCrossGroupBoardAccess(profile.id, next);
      setProfile(updated);
      setSaved(
        next
          ? `${profile.name} can now view other groups' boards.`
          : `${profile.name} can no longer view other groups' boards.`,
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not update board visibility.");
    } finally {
      setBusy(false);
    }
  }

  async function deleteMember() {
    if (!profile) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      await api.deleteMember(profile.id);
      setConfirmDelete(false);
      router.push("/members");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not delete member.");
    } finally {
      setBusy(false);
    }
  }

  if (!profile) {
    return <p className="text-muted">{error ?? "Loading profile…"}</p>;
  }

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <div>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">People</p>
        <h1 className="mt-1 text-4xl">{profile.name}</h1>
        {profile.canViewDetails && profile.role && (
          <p className="mt-1 text-muted">
            {profile.role === "Lead" ? "Team Lead" : "Member"}
            {profile.groupName ? ` · ${profile.groupName}` : ""}
          </p>
        )}
      </div>
      {error && <p className="text-sm text-clay">{error}</p>}
      {saved && <p className="text-sm text-teal">{saved}</p>}

      {!profile.canViewDetails ? (
        <div className="card p-6">
          <p className="text-sm text-muted">Only the name is visible on this profile.</p>
        </div>
      ) : profile.canEditProfile ? (
        <form onSubmit={saveProfile} className="card space-y-4 p-6">
          <label className="block text-sm font-medium">
            Name
            <input className="field mt-1" value={name} onChange={(e) => setName(e.target.value)} required />
          </label>
          <label className="block text-sm font-medium">
            Email
            <input
              type="email"
              className="field mt-1"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>
          <label className="block text-sm font-medium">
            Telegram username
            <input
              className="field mt-1"
              value={telegramUsername}
              onChange={(e) => setTelegramUsername(e.target.value)}
              placeholder="@username (optional)"
            />
          </label>
          <p className="text-sm text-muted">
            Group: {profile.groupName ?? "Unknown"} · Role: {profile.role === "Lead" ? "Team Lead" : "Member"}
          </p>
          <button type="submit" className="btn-primary" disabled={busy}>
            {busy ? "Saving…" : "Save profile"}
          </button>
        </form>
      ) : (
        <dl className="card space-y-3 p-6 text-sm">
          <Detail label="Email">{profile.email ?? "—"}</Detail>
          <Detail label="Telegram">{profile.telegramUsername ? `@${profile.telegramUsername}` : "—"}</Detail>
          <Detail label="Group">{profile.groupName ?? "—"}</Detail>
          <Detail label="Role">{profile.role === "Lead" ? "Team Lead" : "Member"}</Detail>
        </dl>
      )}

      {profile.canChangePassword && (
        <form onSubmit={savePassword} className="card space-y-4 p-6">
          <h2 className="text-2xl">Password</h2>
          <p className="text-sm text-muted">Only you can change this password.</p>
          <label className="block text-sm font-medium">
            Current password
            <input
              type="password"
              className="field mt-1"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
            />
          </label>
          <label className="block text-sm font-medium">
            New password
            <input
              type="password"
              className="field mt-1"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              minLength={8}
            />
          </label>
          <button type="submit" className="btn-primary" disabled={busy}>
            Update password
          </button>
        </form>
      )}

      {profile.canSetAsLead && (
        <div className="card space-y-3 p-6">
          <h2 className="text-2xl">Team Lead</h2>
          {profile.role === "Lead" ? (
            <>
              <p className="text-sm text-muted">
                {profile.name} is a Team Lead. Removing this role does not change whether they can view other groups&apos;
                boards.
              </p>
              <button type="button" className="btn-secondary" disabled={busy} onClick={revokeLead}>
                Remove Team Lead
              </button>
            </>
          ) : (
            <>
              <p className="text-sm text-muted">
                Office Management can promote members to Team Lead. To set several people at once, use the Members page.
              </p>
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-1"
                  checked={allowOtherBoards}
                  onChange={(e) => setAllowOtherBoards(e.target.checked)}
                />
                <span>Allow this member to view other groups&apos; boards</span>
              </label>
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-1"
                  checked={allowAssignOtherGroups}
                  onChange={(e) => setAllowAssignOtherGroups(e.target.checked)}
                />
                <span>Allow this member to assign work to other groups</span>
              </label>
              <button type="button" className="btn-primary" disabled={busy} onClick={setAsLead}>
                Set as Team Lead
              </button>
            </>
          )}
        </div>
      )}

      {profile.canSetAsLead && (
        <div className="card space-y-3 p-6">
          <h2 className="text-2xl">Other groups&apos; boards</h2>
          <p className="text-sm text-muted">
            Independent of Team Lead and of assigning work. When on, this member can view other groups&apos; boards, work
            items, and members. Viewing does not grant assign or edit rights.
          </p>
          <p className="text-sm font-medium">
            {profile.canViewOtherGroupBoards
              ? `${profile.name} can currently view other groups' boards.`
              : `${profile.name} cannot currently view other groups' boards.`}
          </p>
          <button type="button" className="btn-secondary" disabled={busy} onClick={toggleOtherBoardAccess}>
            {profile.canViewOtherGroupBoards ? "Turn off board view access" : "Turn on board view access"}
          </button>
        </div>
      )}

      {profile.canSetAsLead && (
        <div className="card space-y-3 p-6">
          <h2 className="text-2xl">Assign work to other groups</h2>
          <p className="text-sm text-muted">
            Independent of Team Lead and of board view. When on, this member can create and assign work to groups other
            than their own. Office Management always has this ability.
          </p>
          <p className="text-sm font-medium">
            {profile.canAssignWorkToOtherGroups
              ? `${profile.name} can currently assign work to other groups.`
              : `${profile.name} cannot currently assign work to other groups.`}
          </p>
          <button type="button" className="btn-secondary" disabled={busy} onClick={toggleOtherAssignmentAccess}>
            {profile.canAssignWorkToOtherGroups
              ? "Turn off cross-group assignment"
              : "Turn on cross-group assignment"}
          </button>
        </div>
      )}

      {profile.canDeleteMember && (
        <div className="card space-y-3 p-6">
          <h2 className="text-2xl">Delete member</h2>
          <p className="text-sm text-muted">
            Removes {profile.name} from the system. Assigned work is moved to Unassigned. Boards and work items they
            created are kept.
          </p>
          <button
            type="button"
            className="btn-danger"
            disabled={busy}
            onClick={() => {
              setError(null);
              setConfirmDelete(true);
            }}
          >
            Delete member
          </button>
        </div>
      )}

      {(isLead || isOfficeManagement) && (
        <button type="button" className="text-sm font-semibold text-teal hover:underline" onClick={() => router.push("/members")}>
          All members
        </button>
      )}
      {confirmDelete && profile.canDeleteMember && (
        <ConfirmDeleteMemberDialog
          memberName={profile.name}
          busy={busy}
          error={error}
          onCancel={() => {
            if (busy) return;
            setConfirmDelete(false);
          }}
          onConfirm={() => void deleteMember()}
        />
      )}
    </div>
  );
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{label}</dt>
      <dd className="mt-1">{children}</dd>
    </div>
  );
}
