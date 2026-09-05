"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { Group } from "@/lib/types";

const SIDEBAR_KEY = "gg.sidebarCollapsed";

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, loading, logout, isLead } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [groups, setGroups] = useState<Group[]>([]);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    if (!loading && !user && pathname !== "/login") router.replace("/login");
  }, [loading, user, pathname, router]);

  useEffect(() => {
    if (!user) return;
    api.groups().then(setGroups).catch(() => setGroups([]));
  }, [user]);

  useEffect(() => {
    try {
      setCollapsed(window.localStorage.getItem(SIDEBAR_KEY) === "1");
    } catch {
      /* ignore */
    }
  }, []);

  function toggleCollapsed() {
    setCollapsed((value) => {
      const next = !value;
      window.localStorage.setItem(SIDEBAR_KEY, next ? "1" : "0");
      return next;
    });
  }

  if (pathname === "/login") return <>{children}</>;
  if (loading || !user) {
    return (
      <div className="grid min-h-screen place-items-center text-muted">
        Loading the board…
      </div>
    );
  }

  const visibleGroups = isLead ? groups : groups.filter((g) => g.id === user.groupId);

  return (
    <div className="flex min-h-screen">
      <aside
        className={`sticky top-0 flex h-screen shrink-0 flex-col border-r border-line bg-sidebar transition-[width] duration-200 ${
          collapsed ? "w-[4.75rem]" : "w-64"
        }`}
      >
        <div className={`flex items-center gap-3 px-4 py-6 ${collapsed ? "justify-center" : ""}`}>
          <div className="grid h-10 w-10 shrink-0 place-items-center rounded-2xl bg-teal text-sm font-bold text-white">
            GG
          </div>
          {!collapsed && (
            <div className="min-w-0">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-teal">Activity</p>
              <p className="truncate text-base font-semibold">GG Team</p>
            </div>
          )}
        </div>

        <nav className="flex flex-1 flex-col gap-1 overflow-y-auto px-3">
          <NavLink href="/" active={pathname === "/"} collapsed={collapsed} icon={<HomeIcon />}>
            Dashboard
          </NavLink>
          <NavLink
            href="/activity"
            active={pathname.startsWith("/activity")}
            collapsed={collapsed}
            icon={<PulseIcon />}
          >
            Activity
          </NavLink>
          <NavLink
            href="/notifications"
            active={pathname.startsWith("/notifications")}
            collapsed={collapsed}
            icon={<BellIcon />}
          >
            Notifications
          </NavLink>

          <p
            className={`mt-5 mb-1 px-3 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted ${
              collapsed ? "text-center px-0" : ""
            }`}
          >
            {collapsed ? "—" : "Boards"}
          </p>
          {visibleGroups.map((group) => (
            <NavLink
              key={group.id}
              href={`/board/${group.id}`}
              active={pathname === `/board/${group.id}`}
              collapsed={collapsed}
              icon={<BoardIcon />}
            >
              {group.name}
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto border-t border-line px-3 py-4">
          {!collapsed && (
            <div className="mb-3 px-2">
              <p className="truncate text-sm font-semibold">{user.name}</p>
              <p className="text-xs text-muted">{user.role}</p>
            </div>
          )}
          <div className={`flex ${collapsed ? "flex-col" : ""} gap-1`}>
            <button
              onClick={toggleCollapsed}
              className="flex w-full items-center gap-3 rounded-xl px-3 py-2 text-sm text-muted hover:bg-teal-soft hover:text-teal"
              title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            >
              <CollapseIcon flipped={collapsed} />
              {!collapsed && <span>Collapse</span>}
            </button>
            <button
              className="flex w-full items-center gap-3 rounded-xl px-3 py-2 text-sm text-clay hover:bg-paper"
              onClick={() => {
                logout();
                router.push("/login");
              }}
              title="Sign out"
            >
              <SignOutIcon />
              {!collapsed && <span>Sign out</span>}
            </button>
          </div>
        </div>
      </aside>
      <main className="min-w-0 flex-1 px-6 py-8 lg:px-10">{children}</main>
    </div>
  );
}

function NavLink({
  href,
  active,
  collapsed,
  icon,
  children,
}: {
  href: string;
  active: boolean;
  collapsed: boolean;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      title={typeof children === "string" ? children : undefined}
      onClick={(event) => {
        if (typeof window !== "undefined" && window.__ggHasUnsavedChanges) {
          if (!window.confirm("You have unsaved changes. Leave this board and discard them?")) {
            event.preventDefault();
          }
        }
      }}
      className={`flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition ${
        collapsed ? "justify-center" : ""
      } ${active ? "bg-teal text-white shadow-sm" : "text-ink/80 hover:bg-teal-soft hover:text-teal"}`}
    >
      <span className="shrink-0">{icon}</span>
      {!collapsed && <span className="truncate">{children}</span>}
    </Link>
  );
}

function HomeIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1z" />
    </svg>
  );
}

function PulseIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M3 12h4l2.5-6 5 12 2.5-6H21" />
    </svg>
  );
}

function BellIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M6 9a6 6 0 1 1 12 0c0 7 3 7 3 9H3c0-2 3-2 3-9" />
      <path d="M10 21h4" />
    </svg>
  );
}

function BoardIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <rect x="3" y="4" width="7" height="16" rx="1.5" />
      <rect x="14" y="4" width="7" height="10" rx="1.5" />
    </svg>
  );
}

function CollapseIcon({ flipped }: { flipped: boolean }) {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      className={flipped ? "rotate-180" : ""}
    >
      <path d="M15 6 9 12l6 6" />
    </svg>
  );
}

function SignOutIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M10 7V5a1 1 0 0 1 1-1h8a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-8a1 1 0 0 1-1-1v-2" />
      <path d="M4 12h10M8 8l-4 4 4 4" />
    </svg>
  );
}
