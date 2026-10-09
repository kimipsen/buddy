import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  linkedSignal,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import {
  CalendarItemKind,
  CalendarOccurrence,
  CalendarsService,
} from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  AgendaEntry,
  TaskRun,
  compareOccurrences,
  groupTaskRuns,
  isTaskRun,
  occurrenceKey,
} from '../../../core/task-run';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import {
  DoseStatus,
  MedicineDoseOccurrence,
  MedicinesService,
} from '../../../core/medicines.service';
import {
  PickupAssigneeKind,
  PickupOccurrence,
  PickupSlot,
  PickupsService,
  babysitterName,
  playdateHostName,
} from '../../../core/pickups.service';
import { ProgressService, ProgressSummary } from '../../../core/progress.service';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { UsersService } from '../../../core/users.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ProgressBadge } from '../../../shared/progress-badge/progress-badge';
import { ChildMenu } from './child-menu/child-menu';

const EVENT_KIND: CalendarItemKind = 'Event';
const TASK_KIND: CalendarItemKind = 'Task';

const PENDING: DoseStatus = 'Pending';
const TAKEN: DoseStatus = 'Taken';
const SKIPPED: DoseStatus = 'Skipped';

const MEAL_SLOT_LABELS: Record<MealSlot, string> = {
  Breakfast: 'dashboard.mealplan.slots.breakfast',
  Lunch: 'dashboard.mealplan.slots.lunch',
  Dinner: 'dashboard.mealplan.slots.dinner',
  Snack: 'dashboard.mealplan.slots.snack',
};

const MEAL_SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
const MAX_STARS = 5;
const STARS = Array.from({ length: MAX_STARS }, (_, index) => index + 1);

const GUARDIAN = 0 satisfies PickupAssigneeKind;
const SELF_ESCORT = 1 satisfies PickupAssigneeKind;
const SIBLING = 2 satisfies PickupAssigneeKind;
const PLAYDATE = 3 satisfies PickupAssigneeKind;
const BABYSITTER = 4 satisfies PickupAssigneeKind;

const PICKUP_SLOT_LABELS = {
  DropOff: 'child.home.pickup.slots.dropOff',
  PickUp: 'child.home.pickup.slots.pickUp',
} as const satisfies Record<PickupSlot, string>;

export interface EventView extends CalendarOccurrence {
  isPast: boolean;
  isOngoing: boolean;
  progressPercent: number;
}

// What the badge shows until progress first loads, and if it never does: a brand-new child's
// seedling with no stars.
const STARTING_PROGRESS: ProgressSummary = {
  totalStars: 0,
  unlockedMilestones: [],
  displayIcon: '🌱',
  nextGoalThreshold: 0,
  nextGoalIcon: '🌱',
  goalPosts: [],
};

// Today's calendar occurrences, split into the two kinds the dashboard shows separately.
interface TodaysOccurrences {
  tasks: CalendarOccurrence[];
  events: CalendarOccurrence[];
}

// How often the ongoing-event progress fill and past/done state are recomputed. A minute is
// frequent enough that the fill visibly creeps forward without re-rendering the list every
// few seconds for what's a purely cosmetic indicator.
const NOW_REFRESH_INTERVAL_MS = 60_000;

@Component({
  selector: 'app-child-home',
  imports: [TranslatePipe, RouterLink, LoadingSpinner, ProgressBadge, UserDatePipe, ChildMenu],
  templateUrl: './home.html',
})
export class ChildHome implements OnInit, OnDestroy {
  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);
  private readonly users = inject(UsersService);
  private readonly mealplans = inject(MealplansService);
  private readonly medicines = inject(MedicinesService);
  private readonly calendars = inject(CalendarsService);
  private readonly progressService = inject(ProgressService);

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly babysitterKind = BABYSITTER;
  protected readonly pickupSlotLabels = PICKUP_SLOT_LABELS;

  // Best-effort lookups for pickup assignee names: a failure just falls back to the generic label.
  private readonly guardianList = resource({ loader: () => this.guardians.listMyGuardians() });
  private readonly siblingList = resource({ loader: () => this.guardians.listMySiblings() });

  // Pickups are loaded apart from the rest so a failure there shows no pickups rather than the
  // dashboard's load error.
  private readonly pickupsToday = resource({ loader: () => this.loadTodaysPickups() });
  // Meals, doses, and the calendar each load on their own (ensureCurrentUser is memoized, so they
  // share one lookup of the signed-in child), so one section failing still leaves the others
  // rendered with the load error shown. After a save, a section is only written back while it
  // holds a value and isn't reloading: update() on a loading resource would cancel that load,
  // and on a failed one it throws.
  private readonly meals = resource({
    loader: async () => this.loadMeals((await this.users.ensureCurrentUser()).id, todayIsoDate()),
  });
  private readonly doseList = resource({
    loader: async () => this.loadDoses((await this.users.ensureCurrentUser()).id, todayIsoDate()),
  });
  // Doesn't need the child id, but still waits for it: the calendar never loads for a user who
  // can't be resolved.
  private readonly occurrences = resource({
    loader: async () => {
      await this.users.ensureCurrentUser();
      return this.loadCalendarOccurrences();
    },
  });
  // Progress is a supplementary widget, not core dashboard data -- a failed load just leaves the
  // badge as it was (the starting seedling, or the last progress that loaded), with no error.
  private readonly progress = resource({ loader: () => this.progressService.getMyProgress() });

  protected readonly badge = linkedSignal<ProgressSummary | undefined, ProgressSummary>({
    source: () => (this.progress.hasValue() ? this.progress.value() : undefined),
    computation: (next, previous) => next ?? previous?.value ?? STARTING_PROGRESS,
  });

  protected readonly todaysPickups = computed(() =>
    this.pickupsToday.hasValue() ? this.pickupsToday.value() : [],
  );

  // Pickups, meals, doses, and tasks all feed hasAnything() -- until every one of them has
  // loaded, a light day and a still-loading day would render the same empty-state card.
  protected readonly contentLoading = computed(
    () =>
      this.pickupsToday.isLoading() ||
      this.meals.isLoading() ||
      this.doseList.isLoading() ||
      this.occurrences.isLoading(),
  );

  private readonly entriesBySlot = computed(() =>
    this.meals.hasValue() ? this.meals.value() : {},
  );
  private readonly todaysOccurrences = computed(() =>
    this.occurrences.hasValue() ? this.occurrences.value() : undefined,
  );

  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;
  protected readonly stars = STARS;
  // Only meals actually planned today, in slot order -- unlike the guardian widget, this skips
  // "not planned" filler rows entirely (see the "if any" layout decision in
  // docs/frontend/analysis/child-day-dashboard.md).
  protected readonly mealsToShow = computed(() => {
    const entriesBySlot = this.entriesBySlot();
    return MEAL_SLOTS.map((slot) => entriesBySlot[slot]).filter((entry) => entry !== undefined);
  });

  // Rating today's meals right away (rather than only from the past-weeks planner) so the child
  // doesn't have to remember how a meal was by the time they'd next see it there. The draft starts
  // from the meal's comment on file each time its note opens.
  protected readonly rating = createAction<MealSlot>();
  protected readonly editingSlot = signal<MealSlot | null>(null);
  protected readonly commentDraft = linkedSignal(() => {
    const slot = this.editingSlot();
    return slot === null
      ? ''
      : (untracked(() => this.entriesBySlot()[slot])?.rating?.comment ?? '');
  });

  protected readonly pending = PENDING;
  protected readonly taken = TAKEN;
  protected readonly skipped = SKIPPED;
  protected readonly doses = computed(() =>
    this.doseList.hasValue() ? this.doseList.value() : [],
  );
  protected readonly savingDose = createAction<string>();

  protected readonly tasks = computed(() => this.todaysOccurrences()?.tasks ?? []);
  protected readonly savingTask = createAction<string>();

  // Folds today's flat task occurrences into agenda rows so a template-scheduled task's subtasks
  // (e.g. "brush teeth", "put on pajamas") render nested under their parent's title (e.g. "go to
  // bed") instead of as unrelated, unlabeled checklist items -- mirrors the child calendar's
  // identical grouping (see core/task-run.ts).
  protected readonly groupedTasks = computed<AgendaEntry[]>(() => groupTaskRuns(this.tasks()));

  private readonly events = computed(() => this.todaysOccurrences()?.events ?? []);

  // One message slot for the whole dashboard: a failed rating, dose or task update, else a failed
  // load.
  protected readonly error = computed(() => {
    for (const action of [this.rating, this.savingDose, this.savingTask]) {
      const state = action.state();

      if (state.status === 'error') {
        return state.message;
      }
    }

    const loadFailed = [this.meals, this.doseList, this.occurrences].some((section) =>
      section.error(),
    );
    return loadFailed ? 'child.home.loadError' : null;
  });

  // Ticks on an interval (rather than reading Date.now() directly in the template) so the
  // ongoing-event progress fill and past/done state actually update while the dashboard sits
  // open, instead of only reflecting "now" at the moment the page loaded.
  private readonly now = signal(Date.now());
  private nowIntervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly eventsView = computed<EventView[]>(() => {
    const nowMs = this.now();
    return this.events().map((event) => ({ ...event, ...this.eventProgress(event, nowMs) }));
  });

  // Only when every section is empty do we show the "nothing to show yet" card -- a light day
  // shouldn't render five empty-state messages back to back.
  protected readonly hasAnything = computed(
    () =>
      this.todaysPickups().length > 0 ||
      this.mealsToShow().length > 0 ||
      this.doses().length > 0 ||
      this.tasks().length > 0 ||
      this.events().length > 0,
  );

  ngOnInit(): void {
    this.nowIntervalId = setInterval(() => this.now.set(Date.now()), NOW_REFRESH_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    clearInterval(this.nowIntervalId);
  }

  protected readonly playdateHostName = playdateHostName;
  protected readonly babysitterName = babysitterName;

  protected assigneeName(occurrence: PickupOccurrence): string | null {
    const { assignee } = occurrence;

    if (assignee.kind === GUARDIAN) {
      return (
        (this.guardianList.hasValue() ? this.guardianList.value() : []).find(
          (guardian) => guardian.id === assignee.guardianId,
        )?.name.givenName ?? null
      );
    }

    if (assignee.kind === SIBLING) {
      return (
        (this.siblingList.hasValue() ? this.siblingList.value() : []).find(
          (sibling) => sibling.id === assignee.siblingChildId,
        )?.name.givenName ?? null
      );
    }

    return null;
  }

  protected doseKey(dose: MedicineDoseOccurrence): string {
    return `${dose.medicineId}|${dose.time}`;
  }

  // Compound key distinguishing sibling subtask occurrences of the same template-scheduled run
  // (same itemId, different subtaskId) -- see core/task-run.ts's occurrenceKey for why itemId
  // alone is no longer sufficient once a run can produce more than one occurrence per item.
  protected keyFor(task: CalendarOccurrence): string {
    return occurrenceKey(task);
  }

  protected isRun(entry: AgendaEntry): entry is TaskRun {
    return isTaskRun(entry);
  }

  protected async setDoseStatus(dose: MedicineDoseOccurrence, status: DoseStatus): Promise<void> {
    const key = this.doseKey(dose);

    await this.savingDose.run(
      key,
      async () => {
        const me = await this.users.ensureCurrentUser();
        const updated = await this.medicines.setDoseStatus(
          me.id,
          dose.medicineId,
          dose.date,
          dose.time,
          status,
        );
        if (this.doseList.hasValue() && !this.doseList.isLoading()) {
          this.doseList.set(
            this.doseList
              .value()
              .map((existing) =>
                this.doseKey(existing) === key ? { ...existing, status: updated.status } : existing,
              ),
          );
        }
      },
      'child.home.loadError',
    );
  }

  protected async toggleTask(task: CalendarOccurrence): Promise<void> {
    const key = occurrenceKey(task);
    const isCompleted = !task.isCompleted;

    await this.savingTask.run(
      key,
      async () => {
        await this.calendars.setTaskCompletion(
          task.calendarId,
          task.itemId,
          todayIsoDate(),
          isCompleted,
          task.routine?.subtaskId ?? null,
        );
        if (this.occurrences.hasValue() && !this.occurrences.isLoading()) {
          const current = this.occurrences.value();
          this.occurrences.set({
            ...current,
            tasks: current.tasks.map((existing) =>
              occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
            ),
          });
        }

        // The backend awards/revokes a star as part of the same request that just completed above
        // (see SetTaskCompletionHandler), so re-reading progress now already reflects it -- no
        // local point math to duplicate or get out of sync with milestone thresholds.
        this.progress.reload();
      },
      'child.home.loadError',
    );
  }

  private async loadTodaysPickups(): Promise<PickupOccurrence[]> {
    try {
      const me = await this.users.ensureCurrentUser();
      const today = todayIsoDate();
      return await this.pickups.listSchedule(me.id, today, today);
    } catch {
      return [];
    }
  }

  protected isEditing(entry: MealPlanEntry): boolean {
    return this.editingSlot() === entry.slot;
  }

  protected startEditing(entry: MealPlanEntry): void {
    this.editingSlot.set(entry.slot);
  }

  protected cancelEditing(): void {
    this.editingSlot.set(null);
  }

  protected setComment(value: string): void {
    this.commentDraft.set(value);
  }

  // Tapping a star rates immediately with whatever comment is already on file -- a quick
  // reaction shouldn't require opening the comment form first.
  protected async rate(entry: MealPlanEntry, starCount: number): Promise<void> {
    await this.submitRating(entry, starCount, entry.rating?.comment ?? '');
  }

  protected async saveComment(entry: MealPlanEntry): Promise<void> {
    const starCount = entry.rating?.stars ?? MAX_STARS;
    await this.submitRating(entry, starCount, this.commentDraft().trim());
    this.cancelEditing();
  }

  private async submitRating(
    entry: MealPlanEntry,
    starCount: number,
    comment: string,
  ): Promise<void> {
    if (!this.meals.hasValue()) {
      return;
    }

    await this.rating.run(
      entry.slot,
      async () => {
        const { id: childId } = await this.users.ensureCurrentUser();
        const meal = await this.mealplans.rateMeal(childId, entry.mealId, starCount, comment);
        const myRating = meal.ratings.find((rating) => rating.childId === childId) ?? null;

        if (!this.meals.hasValue() || this.meals.isLoading()) {
          return;
        }

        const next = { ...this.meals.value() };

        for (const [slot, existing] of Object.entries(next)) {
          if (existing.mealId === entry.mealId) {
            next[slot as MealSlot] = { ...existing, rating: myRating };
          }
        }

        this.meals.set(next);
      },
      'child.mealplan.rateError',
    );
  }

  private async loadMeals(
    childId: string,
    today: string,
  ): Promise<Partial<Record<MealSlot, MealPlanEntry>>> {
    const entries = await this.mealplans.listMealPlan({ kind: 'family', childId }, today, today);
    const bySlot: Partial<Record<MealSlot, MealPlanEntry>> = {};

    for (const entry of entries) {
      bySlot[entry.slot] = entry;
    }

    return bySlot;
  }

  private async loadDoses(childId: string, today: string): Promise<MedicineDoseOccurrence[]> {
    const occurrences = await this.medicines.listDoses(childId, today, today);
    return [...occurrences].sort((a, b) => a.time.localeCompare(b.time));
  }

  private async loadCalendarOccurrences(): Promise<TodaysOccurrences> {
    const occurrences = await this.calendars.listTodayOccurrences();

    const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);

    tasks.sort(compareOccurrences);

    const events = occurrences.filter((occurrence) => occurrence.kind === EVENT_KIND);

    events.sort(compareOccurrences);

    return { tasks, events };
  }

  // All-day events have no time of day to measure against, so they never read as past or
  // ongoing here -- they stay "current" for the whole day, same as their allDay badge implies.
  private eventProgress(
    event: CalendarOccurrence,
    nowMs: number,
  ): { isPast: boolean; isOngoing: boolean; progressPercent: number } {
    const { timing } = event;

    if (event.isAllDay || timing.kind !== 0) {
      return { isPast: false, isOngoing: false, progressPercent: 0 };
    }

    const startMs = new Date(timing.startsAt).getTime();
    const endMs = new Date(timing.endsAt).getTime();

    if (nowMs >= endMs) {
      return { isPast: true, isOngoing: false, progressPercent: 100 };
    }

    if (nowMs < startMs) {
      return { isPast: false, isOngoing: false, progressPercent: 0 };
    }

    // startMs <= nowMs < endMs here, so the span is always positive.
    return {
      isPast: false,
      isOngoing: true,
      progressPercent: ((nowMs - startMs) / (endMs - startMs)) * 100,
    };
  }

  // A gradient rather than a separate overlay element -- the card's own background fills in from
  // the left as the event progresses, so it reads as darkening in place like a progress bar.
  protected eventProgressBackground(progressPercent: number): string {
    const clamped = Math.min(100, Math.max(0, progressPercent));
    return `linear-gradient(to right, rgb(203 213 225) ${clamped}%, transparent ${clamped}%)`;
  }
}
