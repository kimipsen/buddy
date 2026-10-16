import { Component, OnDestroy, OnInit, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { addDaysIso, toIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { groupTaskRuns, isTaskRun } from '../../../core/task-run';
import { Card } from '../../../shared/card/card';
import { Icon } from '../../../shared/icon/icon';

// One packing list a child hasn't finished packing for tomorrow.
export interface UnpackedList {
  key: string;
  childId: string;
  childName: string;
  title: string;
  icon: string;
  packed: number;
  total: number;
}

interface LoadedTomorrow {
  date: string;
  children: ChildSummary[];
  occurrences: CalendarOccurrence[];
}

// Re-evaluated once a minute, like the child home, so a list appears at its evening time while
// the dashboard sits open.
const NOW_REFRESH_INTERVAL_MS = 60_000;

function minutesOf(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

// "Packing for tomorrow" (docs/backend/analysis/packing-checklists.md, Question 10): from a packing
// list's eveningBeforeFrom time until midnight, each of the guardian's children's lists for
// tomorrow that isn't fully ticked. Computed here from the occurrences tasks-today already reads
// (assignedTo, isCompleted per item, routine.checklist), so there is no backend query of its own.
// Hidden entirely when nothing is left to pack.
@Component({
  selector: 'app-packing-tomorrow',
  imports: [Icon, RouterLink, TranslatePipe, Card],
  templateUrl: './packing-tomorrow.html',
  host: { class: 'contents' },
})
export class PackingTomorrow implements OnInit, OnDestroy {
  private readonly calendars = inject(CalendarsService);
  private readonly guardians = inject(GuardiansService);

  private readonly now = signal(Date.now());
  // A string, so the per-minute tick only changes it (and reloads) when the date rolls over.
  private readonly tomorrowDate = computed(() => addDaysIso(toIsoDate(new Date(this.now())), 1));
  // Keyed on tomorrow's date, so a dashboard left open overnight loads the new tomorrow.
  private readonly tomorrow = resource({
    params: () => ({ date: this.tomorrowDate() }),
    loader: ({ params }) => this.load(params.date),
  });
  private intervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly lists = computed<UnpackedList[]>(() => {
    const loaded = this.tomorrow.hasValue() ? this.tomorrow.value() : undefined;
    const now = new Date(this.now());

    // Just after midnight the loaded "tomorrow" is today's, until the new one has loaded.
    if (!loaded || loaded.date !== addDaysIso(toIsoDate(now), 1)) {
      return [];
    }

    const minutesNow = now.getHours() * 60 + now.getMinutes();
    const namesById = new Map(loaded.children.map((child) => [child.id, child.name.givenName]));

    return groupTaskRuns(loaded.occurrences)
      .filter(isTaskRun)
      .flatMap((run): UnpackedList[] => {
        const first = run.subtasks[0];
        const from = first.routine?.checklist?.eveningBeforeFrom;
        const childId = first.assignedTo;
        const childName = childId ? namesById.get(childId) : undefined;
        const packed = run.subtasks.filter((item) => item.isCompleted).length;

        if (
          !from ||
          !childId ||
          !childName ||
          minutesNow < minutesOf(from) ||
          packed === run.subtasks.length
        ) {
          return [];
        }

        return [
          {
            key: `${run.calendarId}:${run.itemId}`,
            childId,
            childName,
            title: run.parentTitle,
            icon: run.icon,
            packed,
            total: run.subtasks.length,
          },
        ];
      });
  });

  ngOnInit(): void {
    this.intervalId = setInterval(() => this.now.set(Date.now()), NOW_REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    clearInterval(this.intervalId);
  }

  private async load(date: string): Promise<LoadedTomorrow> {
    const [children, occurrences] = await Promise.all([
      this.guardians.listMyChildren(),
      this.calendars.listOccurrencesInRange(date, date),
    ]);

    return {
      date,
      children,
      occurrences: occurrences.filter(
        (occurrence) => occurrence.kind === 'Task' && !!occurrence.routine?.checklist,
      ),
    };
  }
}
