"use client";

import { monthOptions, yearOptions, type PeriodSelection } from "@/lib/period";
import type { TimePeriod } from "@/lib/types";

const OPTIONS: { id: TimePeriod; label: string }[] = [
  { id: "week", label: "This Week" },
  { id: "month", label: "Month" },
  { id: "year", label: "Year" },
];

export function PeriodToggle({
  value,
  onChange,
}: {
  value: PeriodSelection;
  onChange: (next: PeriodSelection) => void;
}) {
  return (
    <div className="flex w-full flex-col items-stretch gap-2 sm:w-auto sm:items-end">
      <div className="inline-flex w-full rounded-full bg-card p-1 shadow-[0_10px_30px_rgba(28,25,23,0.06)] sm:w-auto">
        {OPTIONS.map((option) => {
          const active = option.id === value.period;
          return (
            <button
              key={option.id}
              type="button"
              onClick={() => onChange({ ...value, period: option.id })}
              className={`min-h-11 flex-1 rounded-full px-3 py-2 text-sm font-semibold transition sm:flex-none sm:px-4 ${
                active ? "bg-teal text-white" : "text-muted hover:text-ink"
              }`}
            >
              {option.label}
            </button>
          );
        })}
      </div>
      {value.period === "month" && (
        <div className="flex flex-wrap justify-end gap-2">
          <select
            className="field w-auto"
            aria-label="Select month"
            value={value.month}
            onChange={(event) => onChange({ ...value, month: Number(event.target.value) })}
          >
            {monthOptions().map((month) => (
              <option key={month.value} value={month.value}>
                {month.label}
              </option>
            ))}
          </select>
          <select
            className="field w-auto"
            aria-label="Select year"
            value={value.year}
            onChange={(event) => onChange({ ...value, year: Number(event.target.value) })}
          >
            {yearOptions(value.year).map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </select>
        </div>
      )}
      {value.period === "year" && (
        <select
          className="field w-auto"
          aria-label="Select year"
          value={value.year}
          onChange={(event) => onChange({ ...value, year: Number(event.target.value) })}
        >
          {yearOptions(value.year).map((year) => (
            <option key={year} value={year}>
              {year}
            </option>
          ))}
        </select>
      )}
    </div>
  );
}
