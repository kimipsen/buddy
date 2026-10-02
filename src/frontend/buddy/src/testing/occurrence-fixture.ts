import { CalendarItemOccurrence, OccurrenceTiming, Routine } from '../app/core/calendars.service';

// Spec fixtures describe an occurrence with flat fields (startsAt/endsAt or dueAt, plus
// subtaskId/parentTitle/parentIcon for a routine subtask); this nests them into the wire shape the
// way the backend's CalendarOccurrenceExpansion does. A startsAt makes the occurrence timed (endsAt
// defaults to startsAt), otherwise it is due at dueAt. Any subtaskId or parentTitle makes it a
// routine subtask, which must be timed, as the backend always sends it. An explicit timing, sortAt
// or routine wins over the flat fields.
export interface FlatOccurrenceFields {
  startsAt?: string | null;
  endsAt?: string | null;
  dueAt?: string | null;
  subtaskId?: string | null;
  parentTitle?: string | null;
  parentIcon?: string | null;
}

type Nested = 'timing' | 'sortAt' | 'routine';

export type FlatOccurrence<T extends CalendarItemOccurrence> = Omit<T, Nested> &
  Partial<Pick<T, Nested>> &
  FlatOccurrenceFields;

export function nestOccurrence<T extends CalendarItemOccurrence>(flat: FlatOccurrence<T>): T {
  const { startsAt, endsAt, dueAt, subtaskId, parentTitle, parentIcon, ...rest } = flat;
  const timing = flat.timing ?? timingFrom(startsAt, endsAt, dueAt);
  const routine =
    flat.routine !== undefined
      ? flat.routine
      : routineFrom(subtaskId, parentTitle, parentIcon ?? flat.icon);

  if (routine !== null && timing.kind !== 0) {
    throw new Error('A routine subtask occurrence is always timed: give it a startsAt.');
  }

  return {
    ...rest,
    timing,
    sortAt: flat.sortAt ?? (timing.kind === 0 ? timing.startsAt : timing.dueAt),
    routine,
  } as T;
}

function timingFrom(
  startsAt: string | null | undefined,
  endsAt: string | null | undefined,
  dueAt: string | null | undefined,
): OccurrenceTiming {
  if (startsAt) {
    return { kind: 0, startsAt, endsAt: endsAt ?? startsAt };
  }

  if (dueAt) {
    return { kind: 1, dueAt };
  }

  throw new Error('An occurrence fixture needs a startsAt or a dueAt.');
}

function routineFrom(
  subtaskId: string | null | undefined,
  parentTitle: string | null | undefined,
  parentIcon: string,
): Routine | null {
  if (!subtaskId && !parentTitle) {
    return null;
  }

  return { subtaskId: subtaskId ?? 'subtask-1', parentTitle: parentTitle ?? '', parentIcon };
}
