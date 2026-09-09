"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
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
      await api.setLeads([profile.id]);
      load();
      setSaved(`${profile.name} is now a Team Lead.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not set Team Lead.");
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
            <p className="text-sm text-muted">{profile.name} is already a Team Lead.</p>
          ) : (
            <>
              <p className="text-sm text-muted">
                Office Management can promote members to Team Lead. To set several people at once, use the Members page.
              </p>
              <button type="button" className="btn-primary" disabled={busy} onClick={setAsLead}>
                Set as Team Lead
              </button>
            </>
          )}
        </div>
      )}

      {(isLead || isOfficeManagement) && (
        <button type="button" className="text-sm font-semibold text-teal hover:underline" onClick={() => router.push("/members")}>
          All members
        </button>
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
