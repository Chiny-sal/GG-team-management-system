"use client";

import Link from "next/link";

export function MemberLink({
  id,
  name,
  className,
}: {
  id: string | null | undefined;
  name: string | null | undefined;
  className?: string;
}) {
  const label = name?.trim() || "Member";
  if (!id) return <span className={className}>{label}</span>;

  return (
    <Link
      href={`/members/${id}`}
      className={`font-semibold text-teal hover:underline ${className ?? ""}`}
      onClick={(event) => event.stopPropagation()}
    >
      {label}
    </Link>
  );
}
