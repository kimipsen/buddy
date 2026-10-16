import { addDaysIso, parseIsoDate, startOfWeekIso } from './date-utils';
import { toMinutes } from './feelings';
import type { FeelingCheckIn, FeelingTag, FeelingZone } from './feelings.service';
import type { MedicineSchedule } from './medicines.service';
import type { SleepEntry } from './sleep-diary.service';
import type { VisitSummaryMedicineSection } from './visit-summaries.service';

// The numbers a doctor reads off a visit summary, computed in the browser from the sections' own
// DTOs: the backend returns the data, never a second copy of these rules
// (docs/backend/analysis/doctor-visit-summary.md, Question 3).

const MINUTES_PER_DAY = 24 * 60;

function average(values: readonly number[]): number | null {
  return values.length === 0 ? null : Math.round(values.reduce((a, b) => a + b, 0) / values.length);
}

export interface SleepStats {
  nights: number;
  averageMinutes: number | null;
  weekdayAverageMinutes: number | null;
  weekendAverageMinutes: number | null;
  // From "in bed" to "fell asleep"; a night with either time missing is left out.
  averageMinutesToFallAsleep: number | null;
  nightWakeUps: number;
  tiredMornings: number;
}

export function sleepStats(entries: readonly SleepEntry[]): SleepStats {
  const totals = (weekend?: boolean) =>
    entries
      .filter((e) => weekend === undefined || e.isWeekend === weekend)
      .flatMap((e) => (e.totalSleepMinutes === null ? [] : [e.totalSleepMinutes]));

  const latencies = entries.flatMap((e) => {
    if (!e.bedTime || !e.fellAsleepTime) {
      return [];
    }

    // Asleep after midnight from an evening bedtime wraps around the day.
    const minutes = toMinutes(e.fellAsleepTime) - toMinutes(e.bedTime);
    return [minutes < 0 ? minutes + MINUTES_PER_DAY : minutes];
  });

  return {
    nights: entries.length,
    averageMinutes: average(totals()),
    weekdayAverageMinutes: average(totals(false)),
    weekendAverageMinutes: average(totals(true)),
    averageMinutesToFallAsleep: average(latencies),
    nightWakeUps: entries.reduce((sum, e) => sum + e.nightWakeUps.length, 0),
    tiredMornings: entries.filter((e) => e.isTired).length,
  };
}

export interface MedicineAdherence {
  schedule: MedicineSchedule;
  taken: number;
  skipped: number;
  // Past doses nobody marked. Doses after `today` are not counted at all.
  notMarked: number;
}

export function medicineAdherence(
  section: VisitSummaryMedicineSection,
  today: string,
): MedicineAdherence[] {
  return section.schedules.map((schedule) => {
    const doses = section.doses.filter((d) => d.medicineId === schedule.id && d.date <= today);

    return {
      schedule,
      taken: doses.filter((d) => d.status === 'Taken').length,
      skipped: doses.filter((d) => d.status === 'Skipped').length,
      notMarked: doses.filter((d) => d.status === 'Pending').length,
    };
  });
}

export type DayPart = 'morning' | 'afternoon' | 'evening';
export const DAY_PARTS: readonly DayPart[] = ['morning', 'afternoon', 'evening'];
export const ZONE_ORDER: readonly FeelingZone[] = ['Green', 'Blue', 'Yellow', 'Red'];

export function dayPartOf(time: string): DayPart {
  const minutes = toMinutes(time);
  return minutes < 12 * 60 ? 'morning' : minutes < 17 * 60 ? 'afternoon' : 'evening';
}

export interface FeelingsStats {
  total: number;
  selfReports: number;
  // Zone counts per part of the day, every zone present (zeros kept) in ZONE_ORDER.
  byDayPart: { part: DayPart; total: number; zones: { zone: FeelingZone; count: number }[] }[];
  // Most used words first; ties keep the order they were first used in.
  topTags: { tag: FeelingTag; count: number }[];
  // The adults' notes, oldest first. A child's own notes never reach the report.
  adultNotes: { date: string; time: string; note: string }[];
}

export function feelingsStats(checkIns: readonly FeelingCheckIn[], maxTags = 8): FeelingsStats {
  const tagCounts = new Map<FeelingTag, number>();

  for (const checkIn of checkIns) {
    for (const tag of checkIn.tags) {
      tagCounts.set(tag, (tagCounts.get(tag) ?? 0) + 1);
    }
  }

  return {
    total: checkIns.length,
    selfReports: checkIns.filter((c) => c.isSelfReport).length,
    byDayPart: DAY_PARTS.map((part) => {
      const inPart = checkIns.filter((c) => dayPartOf(c.time) === part);
      return {
        part,
        total: inPart.length,
        zones: ZONE_ORDER.map((zone) => ({
          zone,
          count: inPart.filter((c) => c.zone === zone).length,
        })),
      };
    }),
    topTags: [...tagCounts.entries()]
      .map(([tag, count]) => ({ tag, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, maxTags),
    adultNotes: checkIns
      .filter((c) => !c.isSelfReport && c.note.trim() !== '')
      .map((c) => ({ date: c.date, time: c.time.slice(0, 5), note: c.note }))
      .sort((a, b) => `${a.date}${a.time}`.localeCompare(`${b.date}${b.time}`)),
  };
}

export interface OverviewDay {
  date: string;
  // False for the padding days that fill the first and last week.
  inPeriod: boolean;
  sleepMinutes: number | null;
  doses: { taken: number; due: number } | null;
  zones: FeelingZone[];
  // The day the regimen differs from the day before (a dose change), per the effect notes.
  doseChange: boolean;
}

// The period as whole weeks, Monday first: one row per week reads and prints well from 7 to 92
// days, where one column per day would not.
export function overviewWeeks(
  from: string,
  to: string,
  sleep: readonly SleepEntry[] | null,
  medicine: VisitSummaryMedicineSection | null,
  checkIns: readonly FeelingCheckIn[] | null,
  today: string,
): OverviewDay[][] {
  const sleepByDate = new Map(sleep?.map((e) => [e.date, e.totalSleepMinutes]) ?? []);
  const regimenByDate = new Map(
    medicine?.effectNotes.map((n) => [
      n.date,
      n.regimen
        .map((m) => `${m.name} ${m.dosage}`)
        .sort()
        .join('|'),
    ]) ?? [],
  );
  const weeks: OverviewDay[][] = [];
  let previousRegimen: string | undefined;

  for (let start = startOfWeekIso(from); start <= to; start = addDaysIso(start, 7)) {
    weeks.push(
      Array.from({ length: 7 }, (_, offset) => {
        const date = addDaysIso(start, offset);
        const inPeriod = date >= from && date <= to;
        const due = medicine?.doses.filter((d) => d.date === date && d.date <= today) ?? [];
        const regimen = regimenByDate.get(date);
        const doseChange =
          inPeriod &&
          regimen !== undefined &&
          previousRegimen !== undefined &&
          regimen !== previousRegimen;

        if (inPeriod && regimen !== undefined) {
          previousRegimen = regimen;
        }

        return {
          date,
          inPeriod,
          sleepMinutes: inPeriod ? (sleepByDate.get(date) ?? null) : null,
          doses:
            inPeriod && medicine && due.length > 0
              ? { taken: due.filter((d) => d.status === 'Taken').length, due: due.length }
              : null,
          zones: inPeriod
            ? (checkIns ?? [])
                .filter((c) => c.date === date)
                .sort((a, b) => a.time.localeCompare(b.time))
                .map((c) => c.zone)
            : [],
          doseChange,
        };
      }),
    );
  }

  return weeks;
}

export function periodDays(from: string, to: string): number {
  return Math.round((parseIsoDate(to).getTime() - parseIsoDate(from).getTime()) / 86_400_000) + 1;
}
