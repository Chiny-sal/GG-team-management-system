"use client";

import type { TimePeriod } from "@/lib/types";

const OPTIONS: { id: TimePeriod; label: string }[] = [
  { id: "week", label: "This Week" },
  { id: "month", label: "This Month" },
  { id: "year", label: "This Year" },
];

export function PeriodToggle({
  value,
  onChange,
}: {
  value: TimePeriod;
  onChange: (period: TimePeriod) => void;
}) {
  return (
    <div className="inline-flex rounded-full bg-card p-1 shadow-[0_10px_30px_rgba(28,25,23,0.06)]">
      {OPTIONS.map((option) => {
        const active = option.id === value;
        return (
          <button
            key={option.id}
            type="button"
            onClick={() => onChange(option.id)}
            className={`rounded-full px-4 py-2 text-sm font-semibold transition ${
              active ? "bg-teal text-white" : "text-muted hover:text-ink"
            }`}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
