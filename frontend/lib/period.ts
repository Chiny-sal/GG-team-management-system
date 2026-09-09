import type { TimePeriod } from "./types";

const MONTH_NAMES = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
] as const;

export type PeriodSelection = {
  period: TimePeriod;
  year: number;
  month: number;
};

export function currentPeriodSelection(): PeriodSelection {
  const now = new Date();
  return { period: "week", year: now.getFullYear(), month: now.getMonth() + 1 };
}

export function yearOptions(selectedYear: number) {
  const current = new Date().getFullYear();
  const start = Math.min(2024, current - 5, selectedYear);
  const end = Math.max(current + 1, selectedYear);
  const years: number[] = [];
  for (let year = end; year >= start; year -= 1) years.push(year);
  return years;
}

export function monthOptions() {
  return MONTH_NAMES.map((label, index) => ({ value: index + 1, label }));
}

export function periodSearchParams(selection: PeriodSelection, weekId?: string) {
  const params = new URLSearchParams();
  if (selection.period !== "week") params.set("period", selection.period);
  if (selection.period === "month" || selection.period === "year") {
    params.set("year", String(selection.year));
  }
  if (selection.period === "month") params.set("month", String(selection.month));
  if (weekId) params.set("weekId", weekId);
  return params;
}
