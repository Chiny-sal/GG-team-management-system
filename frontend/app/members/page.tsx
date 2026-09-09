"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { MemberWorkSummary } from "@/lib/types";

export default function MembersDirectoryPage() {
  const { user, isLead, isOfficeManagement } = useAuth();
  const [rows, setRows] = useState<MemberWorkSummary[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState("");

  const canOpen = isLead || isOfficeManagement;

  const load = useCallback(() => {
    if (!user || !canOpen) return;
    api.membersDirectory().then(setRows).catch((e: Error) => setError(e.message));
  }, [user, canOpen]);

  useEffect(() => {
    load();
  }, [load]);

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter((row) =>
      [row.name, row.groupName, row.role].join(" ").toLowerCase().includes(needle),
    );
  }, [rows, search]);

  function toggle(id: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function setSelectedAsLeads() {
    const ids = [...selected];
    if (ids.length === 0) return;
    if (!window.confirm(`Set ${ids.length} member${ids.length === 1 ? "" : "s"} as Team Lead?`)) return;
    setBusy(true);
    setError(null);
    try {
      await api.setLeads(ids);
      setSelected(new Set());
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not set Team Leads.");
    } finally {
      setBusy(false);
    }
  }

  async function exportSheet() {
    setBusy(true);
    setError(null);
    try {
      await api.downloadMembersExport();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Export failed.");
    } finally {
      setBusy(false);
    }
  }

  if (!canOpen) {
    return <p className="text-muted">Only Team Leads and Office Management can view this page.</p>;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-teal">People</p>
          <h1 className="mt-1 text-4xl">Members</h1>
          <p className="mt-1 text-muted">Work assigned to each person, across every group.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {isOfficeManagement && (
            <button
              type="button"
              className="btn-secondary"
              disabled={busy || selected.size === 0}
              onClick={setSelectedAsLeads}
            >
              Set as Team Lead{selected.size > 0 ? ` (${selected.size})` : ""}
            </button>
          )}
          <button type="button" className="btn-primary" disabled={busy} onClick={exportSheet}>
            Export to Excel
          </button>
        </div>
      </div>
      <input
        className="field max-w-md"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search members…"
      />
      {error && <p className="text-sm text-clay">{error}</p>}
      <div className="card overflow-x-auto">
        <table className="w-full min-w-[48rem] text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs uppercase tracking-[0.12em] text-muted">
              {isOfficeManagement && <th className="px-4 py-3" />}
              <th className="px-4 py-3">Member</th>
              <th className="px-4 py-3">Group</th>
              <th className="px-4 py-3">Role</th>
              <th className="px-4 py-3">Assigned</th>
              <th className="px-4 py-3">Ongoing</th>
              <th className="px-4 py-3">Done</th>
              <th className="px-4 py-3">Not done</th>
              <th className="px-4 py-3">Total</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((row) => (
              <tr key={row.id} className="border-b border-line last:border-0">
                {isOfficeManagement && (
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={selected.has(row.id)}
                      onChange={() => toggle(row.id)}
                      aria-label={`Select ${row.name}`}
                    />
                  </td>
                )}
                <td className="px-4 py-3">
                  <Link href={`/members/${row.id}`} className="font-semibold text-teal hover:underline">
                    {row.name}
                  </Link>
                </td>
                <td className="px-4 py-3">{row.groupName}</td>
                <td className="px-4 py-3">{row.role === "Lead" ? "Team Lead" : "Member"}</td>
                <td className="px-4 py-3">{row.assignedCount}</td>
                <td className="px-4 py-3">{row.ongoingCount}</td>
                <td className="px-4 py-3">{row.doneCount}</td>
                <td className="px-4 py-3">{row.notDoneCount}</td>
                <td className="px-4 py-3 font-semibold">{row.totalAssigned}</td>
              </tr>
            ))}
            {visible.length === 0 && (
              <tr>
                <td className="px-4 py-6 text-muted" colSpan={isOfficeManagement ? 9 : 8}>
                  No members match that search.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
