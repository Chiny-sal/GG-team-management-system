"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { Group } from "@/lib/types";

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, loading, logout, isLead } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [groups, setGroups] = useState<Group[]>([]);

  useEffect(() => {
    if (!loading && !user && pathname !== "/login") router.replace("/login");
  }, [loading, user, pathname, router]);

  useEffect(() => {
    if (!user) return;
    api.groups().then(setGroups).catch(() => setGroups([]));
  }, [user]);

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
    <div className="min-h-screen">
      <header className="border-b border-line bg-card/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-6 px-6 py-4">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-teal">Community ledger</p>
            <Link href="/" className="serif text-2xl">
              GG Activity
            </Link>
          </div>
          <nav className="flex flex-wrap items-center gap-4 text-sm">
            <NavLink href="/" active={pathname === "/"}>
              Dashboard
            </NavLink>
            <NavLink href="/activity" active={pathname.startsWith("/activity")}>
              Activity
            </NavLink>
            <NavLink href="/notifications" active={pathname.startsWith("/notifications")}>
              Notifications
            </NavLink>
            {visibleGroups.map((group) => (
              <NavLink
                key={group.id}
                href={`/board/${group.id}`}
                active={pathname === `/board/${group.id}`}
              >
                {group.name}
              </NavLink>
            ))}
          </nav>
          <div className="text-right text-sm">
            <p className="font-medium">{user.name}</p>
            <p className="text-muted">{user.role}</p>
            <button className="mt-1 text-clay underline" onClick={() => { logout(); router.push("/login"); }}>
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-6 py-8">{children}</main>
    </div>
  );
}

function NavLink({
  href,
  active,
  children,
}: {
  href: string;
  active: boolean;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className={`rounded-full px-3 py-1 ${active ? "bg-teal text-white" : "text-ink hover:bg-teal-soft"}`}
    >
      {children}
    </Link>
  );
}
