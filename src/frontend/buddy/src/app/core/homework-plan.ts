import type { Schemas } from './api/schemas';

// Splitting tonight's homework into timed focus chunks with breaks
// (docs/frontend/analysis/homework-splitter.md, Question 3). Pure functions only.

// The guardian's per-child lengths, from GET /users/me (child) or ChildSummary (guardian).
export type HomeworkChunks = Schemas['HomeworkChunks'];

export const DEFAULT_HOMEWORK_CHUNKS: HomeworkChunks = { focusMinutes: 15, breakMinutes: 5 };

// The same ranges UpdateChildHomeworkChunksValidator enforces.
export const FOCUS_MINUTES_RANGE = { min: 5, max: 45 } as const;
export const BREAK_MINUTES_RANGE = { min: 1, max: 15 } as const;

export const MAX_PARTS = 6;
export const MAX_PART_TITLE_LENGTH = 60;
export const ESTIMATE_STEP_MINUTES = 5;
export const MIN_ESTIMATE_MINUTES = 5;
export const MAX_ESTIMATE_MINUTES = 120;

export interface HomeworkPart {
  id: string;
  title: string;
  estimateMinutes: number;
}

export type HomeworkStep =
  | { kind: 'focus'; partId: string; index: number; of: number; minutes: number }
  | { kind: 'break'; minutes: number };

// `total` minutes as ceil(total / focus) near-equal whole-minute chunks, the larger ones first:
// 40 at 15 -> [14, 13, 13].
export function splitMinutes(total: number, focusMinutes: number): number[] {
  const count = Math.max(1, Math.ceil(total / focusMinutes));
  const base = Math.floor(total / count);
  const larger = total - base * count;
  return Array.from({ length: count }, (_, i) => (i < larger ? base + 1 : base));
}

// Every part's chunks in order, with a break between every two focus chunks and none at the end.
export function planHomework(parts: HomeworkPart[], chunks: HomeworkChunks): HomeworkStep[] {
  const focusSteps = parts.flatMap((part) => {
    const minutes = splitMinutes(part.estimateMinutes, chunks.focusMinutes);
    return minutes.map((m, i): HomeworkStep => ({
      kind: 'focus',
      partId: part.id,
      index: i + 1,
      of: minutes.length,
      minutes: m,
    }));
  });

  return withBreaks(focusSteps, chunks.breakMinutes);
}

// Puts a break of `breakMinutes` between every two focus steps.
export function withBreaks(focusSteps: HomeworkStep[], breakMinutes: number): HomeworkStep[] {
  return focusSteps.flatMap((step, i) =>
    i === 0 ? [step] : [{ kind: 'break', minutes: breakMinutes } as HomeworkStep, step],
  );
}

export function totalMinutes(steps: HomeworkStep[]): number {
  return steps.reduce((sum, step) => sum + step.minutes, 0);
}

// A part the plan form may start with: a title and an estimate inside the allowed range.
export function isValidPart(part: HomeworkPart): boolean {
  const title = part.title.trim();
  return (
    title.length > 0 &&
    title.length <= MAX_PART_TITLE_LENGTH &&
    part.estimateMinutes >= MIN_ESTIMATE_MINUTES &&
    part.estimateMinutes <= MAX_ESTIMATE_MINUTES &&
    part.estimateMinutes % ESTIMATE_STEP_MINUTES === 0
  );
}
