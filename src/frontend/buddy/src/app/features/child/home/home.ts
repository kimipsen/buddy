import {
  Component,
  OnDestroy,
  OnInit,
  computed,
  effect,
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
import { ChildClockService } from '../../../core/child-clock.service';
import { completeChildTask } from '../../../core/child-task-completion';
import { ChildTimerService } from '../../../core/child-timer.service';
import { FeaturesService } from '../../../core/features.service';
import { GuardiansService, childFacingName } from '../../../core/guardians.service';
import { currentStep } from '../../../core/homework-session';
import { readHomeworkSession } from '../../../core/homework-storage';
import { HouseRulesService } from '../../../core/house-rules.service';
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
import { TaskLibraryService, TaskTemplate } from '../../../core/task-library.service';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { UsersService } from '../../../core/users.service';
import { TimerState, currentRun, timerState } from '../../../core/visual-timer';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ProgressBadge } from '../../../shared/progress-badge/progress-badge';
import { VisualTimer } from '../../../shared/visual-timer/visual-timer';
import { ChildPage } from '../child-page/child-page';
import { ChildMenu } from './child-menu/child-menu';
import { ChildSuggestions } from './child-suggestions/child-suggestions';
import { FeelingsCheckIn } from './feelings-check-in/feelings-check-in';

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
  spendableStars: 0,
  spentStars: 0,
  rewards: [],
  rewardRequests: [],
};

// A packing list's run (docs/backend/analysis/packing-checklists.md): its items are all due when
// the child leaves the house, so it renders as a checklist with an "n of m packed" count and gets
// no time-left timer.
export function isChecklistRun(run: TaskRun): boolean {
  return (run.subtasks[0].routine?.checklist ?? null) !== null;
}

// Minutes since midnight of a "HH:mm" or "HH:mm:ss" time.
function minutesOf(time: string): number {
  const [hours = 0, minutes = 0] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

// Tomorrow's packing-list occurrences, and the date they were loaded for.
interface TomorrowsChecklists {
  date: string;
  tasks: CalendarOccurrence[];
}

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
  imports: [
    TranslatePipe,
    RouterLink,
    LoadingSpinner,
    ProgressBadge,
    UserDatePipe,
    ChildMenu,
    ChildPage,
    VisualTimer,
    ChildSuggestions,
    FeelingsCheckIn,
  ],
  templateUrl: './home.html',
})
export class ChildHome implements OnInit, OnDestroy {
  protected readonly features = inject(FeaturesService);

  private readonly guardians = inject(GuardiansService);
  private readonly pickups = inject(PickupsService);
  private readonly users = inject(UsersService);
  private readonly mealplans = inject(MealplansService);
  private readonly medicines = inject(MedicinesService);
  private readonly calendars = inject(CalendarsService);
  private readonly progressService = inject(ProgressService);
  private readonly houseRules = inject(HouseRulesService);
  private readonly childTimer = inject(ChildTimerService);
  // "Today" is the child's own (profile time zone), not the device's: every date below comes from it.
  private readonly clock = inject(ChildClockService);
  private readonly taskLibrary = inject(TaskLibraryService);

  // A homework session left in progress on this device: the home shows a way back to it
  // (docs/frontend/analysis/homework-splitter.md, Question 5).
  protected readonly homeworkInProgress = (() => {
    const session = readHomeworkSession(this.clock.today());
    const step = session ? currentStep(session) : null;
    if (!session || !step) {
      return null;
    }

    return step.kind === 'focus'
      ? {
          isBreak: false,
          title: session.parts.find((part) => part.id === step.partId)?.title ?? '',
          index: step.index,
          of: step.of,
        }
      : { isBreak: true, title: '', index: 0, of: 0 };
  })();

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
    loader: async () =>
      this.loadMeals((await this.users.ensureCurrentUser()).id, this.clock.today()),
  });
  private readonly doseList = resource({
    loader: async () =>
      this.loadDoses((await this.users.ensureCurrentUser()).id, this.clock.today()),
  });
  // Doesn't need the child id, but still waits for it: the calendar never loads for a user who
  // can't be resolved.
  private readonly occurrences = resource({
    loader: async () => {
      await this.users.ensureCurrentUser();
      return this.loadCalendarOccurrences();
    },
  });
  // Tomorrow's packing lists, shown in "Pack for tomorrow" from each list's eveningBeforeFrom time
  // today. Supplementary like progress: a failed load just leaves the section out. Today's tasks
  // keep their own (shared, memoized) request, so this is one more per calendar.
  // Keyed on tomorrow's date, so a home left open overnight loads the new tomorrow after midnight.
  private readonly tomorrow = resource({
    params: () => ({ date: this.tomorrowDate() }),
    loader: async ({ params: { date } }): Promise<TomorrowsChecklists> => {
      await this.users.ensureCurrentUser();
      const occurrences = await this.calendars.listOccurrencesInRange(date, date);
      const tasks = occurrences.filter(
        (occurrence) => occurrence.kind === TASK_KIND && !!occurrence.routine?.checklist,
      );
      tasks.sort(compareOccurrences);
      return { date, tasks };
    },
  });

  // Progress is a supplementary widget, not core dashboard data -- a failed load just leaves the
  // badge as it was (the starting seedling, or the last progress that loaded), with no error.
  // Not asked for at all while progress is turned off: the badge isn't shown.
  private readonly progress = resource({
    loader: () =>
      this.features.enabled('progress')
        ? this.progressService.getMyProgress()
        : Promise.resolve(undefined),
  });

  // How many house rules wait for "I've read this". Supplementary like progress: a failed load
  // just leaves the card out, and nothing is asked for while house rules are turned off.
  private readonly rulesToRead = resource({
    loader: async () =>
      this.features.enabled('houseRules')
        ? (await this.houseRules.getChildRules((await this.users.ensureCurrentUser()).id))
            .pendingAcknowledgements
        : 0,
  });
  protected readonly pendingRules = computed(() =>
    this.rulesToRead.hasValue() ? this.rulesToRead.value() : 0,
  );

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
  protected readonly now = signal(Date.now());
  private nowIntervalId: ReturnType<typeof setInterval> | undefined;

  protected readonly eventsView = computed<EventView[]>(() => {
    const nowMs = this.now();
    return this.events().map((event) => ({ ...event, ...this.eventProgress(event, nowMs) }));
  });

  // Visual time-left timers (docs/frontend/analysis/visual-time-left-timer.md). Off until the
  // signed-in child has loaded, so a child whose guardian turned them off never sees one flash.
  private readonly me = resource({ loader: () => this.users.ensureCurrentUser() });
  protected readonly timersOn = computed(
    () => this.me.hasValue() && this.me.value().showVisualTimers !== false,
  );

  // The one run that gets a timer, and its state. Only that run's header and current subtask show
  // it; a finished run shows none.
  protected readonly timerRun = computed(() =>
    this.timersOn()
      ? currentRun(
          this.groupedTasks()
            .filter(isTaskRun)
            .filter((run) => !isChecklistRun(run)),
          this.now(),
          this.childTimer.stored()?.itemId,
        )
      : null,
  );
  protected readonly timer = computed<TimerState | null>(() => {
    const run = this.timerRun();
    if (!run) {
      return null;
    }

    const state = timerState(run, this.now(), this.childTimer.anchorFor(run.itemId));
    return state.phase === 'done' ? null : state;
  });

  // Tomorrow's packing lists whose evening time has passed on the child's clock. The date check
  // hides the old list while the new tomorrow is loading after midnight.
  protected readonly packForTomorrow = computed<TaskRun[]>(() => {
    const loaded = this.tomorrow.hasValue() ? this.tomorrow.value() : undefined;
    const now = this.now();

    if (!loaded || loaded.date !== this.clock.tomorrow(now)) {
      return [];
    }

    const minutesNow = this.clock.minutesOfDay(now);
    return groupTaskRuns(loaded.tasks)
      .filter(isTaskRun)
      .filter((run) => {
        const from = run.subtasks[0].routine?.checklist?.eveningBeforeFrom;
        return !!from && minutesNow >= minutesOf(from);
      });
  });

  // The child's own packing-list templates, for the suggestions under each list. Asked for only
  // while a packing list is on screen and the task library is on.
  private readonly checklistOnScreen = computed(
    () =>
      this.features.enabled('taskLibrary') &&
      (this.packForTomorrow().length > 0 ||
        this.groupedTasks().some((entry) => isTaskRun(entry) && isChecklistRun(entry))),
  );
  private readonly checklistTemplates = resource({
    params: () => (this.checklistOnScreen() ? true : undefined),
    loader: async () =>
      this.taskLibrary.fetchTaskTemplates((await this.users.ensureCurrentUser()).id),
  });

  protected readonly isChecklistRun = isChecklistRun;
  protected readonly tomorrowDate = computed(() => this.clock.tomorrow(this.now()));

  // Only when every section is empty do we show the "nothing to show yet" card -- a light day
  // shouldn't render five empty-state messages back to back.
  protected readonly hasAnything = computed(
    () =>
      this.packForTomorrow().length > 0 ||
      this.todaysPickups().length > 0 ||
      this.mealsToShow().length > 0 ||
      this.doses().length > 0 ||
      this.tasks().length > 0 ||
      this.events().length > 0,
  );

  constructor() {
    // Recalculate the moment the current step's time runs out, rather than at the next minute
    // tick, so the buttons (Pause) follow the disc straight away.
    effect((onCleanup) => {
      const deadlineMs = this.timer()?.deadlineMs;
      if (deadlineMs === null || deadlineMs === undefined) {
        return;
      }

      const id = setTimeout(() => this.now.set(Date.now()), Math.max(0, deadlineMs - Date.now()));
      onCleanup(() => clearTimeout(id));
    });

    // A guardian turned timers off: forget any child-started timer, so turning them back on later
    // starts from the schedule rather than a stale Start.
    effect(() => {
      if (this.me.hasValue() && this.me.value().showVisualTimers === false) {
        this.childTimer.stop();
      }
    });
  }

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
      const guardian = (this.guardianList.hasValue() ? this.guardianList.value() : []).find(
        (candidate) => candidate.id === assignee.guardianId,
      );
      return guardian ? childFacingName(guardian) : null;
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

  protected packedCount(run: TaskRun): number {
    return run.subtasks.filter((subtask) => subtask.isCompleted).length;
  }

  // The template a packing-list run was scheduled from, found by its items' ids (an occurrence
  // doesn't carry the template id). Undefined while loading, or with the task library off.
  protected templateFor(run: TaskRun): TaskTemplate | undefined {
    const subtaskId = run.subtasks[0].routine?.subtaskId;
    const templates = this.checklistTemplates.hasValue() ? this.checklistTemplates.value() : [];
    return templates.find((template) =>
      template.subtasks.some((subtask) => subtask.id === subtaskId),
    );
  }

  protected onSuggestionsChanged(updated: TaskTemplate): void {
    if (this.checklistTemplates.hasValue() && !this.checklistTemplates.isLoading()) {
      this.checklistTemplates.set(
        this.checklistTemplates
          .value()
          .map((template) => (template.id === updated.id ? updated : template)),
      );
    }
  }

  // Ticks an item of tomorrow's packing list: the occurrence is tomorrow's, which the backend
  // allows a day ahead for packing lists only.
  protected async toggleTomorrowTask(task: CalendarOccurrence): Promise<void> {
    const key = occurrenceKey(task);
    const isCompleted = !task.isCompleted;
    const date = this.tomorrow.hasValue() ? this.tomorrow.value().date : this.tomorrowDate();

    await this.savingTask.run(
      key,
      async () => {
        await completeChildTask(this.calendars, task, isCompleted, date);
        if (this.tomorrow.hasValue() && !this.tomorrow.isLoading()) {
          const current = this.tomorrow.value();
          this.tomorrow.set({
            ...current,
            tasks: current.tasks.map((existing) =>
              occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
            ),
          });
        }
        this.progress.reload();
      },
      'child.home.loadError',
    );
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

  protected isCurrentSubtask(subtask: CalendarOccurrence): boolean {
    const current = this.timer()?.current;
    return (
      current !== null && current !== undefined && occurrenceKey(current) === occurrenceKey(subtask)
    );
  }

  protected startTimer(run: TaskRun): void {
    this.childTimer.start(run);
    this.now.set(Date.now());
  }

  protected pauseTimer(run: TaskRun): void {
    this.childTimer.pause(run.itemId);
    this.now.set(Date.now());
  }

  protected resumeTimer(run: TaskRun): void {
    this.childTimer.resume(run.itemId);
    this.now.set(Date.now());
  }

  protected stopTimer(): void {
    this.childTimer.stop();
    this.now.set(Date.now());
  }

  protected async toggleTask(task: CalendarOccurrence): Promise<void> {
    const key = occurrenceKey(task);
    const isCompleted = !task.isCompleted;

    await this.savingTask.run(
      key,
      async () => {
        await completeChildTask(this.calendars, task, isCompleted, this.clock.today());
        if (this.occurrences.hasValue() && !this.occurrences.isLoading()) {
          const current = this.occurrences.value();
          this.occurrences.set({
            ...current,
            tasks: current.tasks.map((existing) =>
              occurrenceKey(existing) === key ? { ...existing, isCompleted } : existing,
            ),
          });

          // A child-started timer moves on to the next step (or back to an unticked one).
          const run = this.groupedTasks()
            .filter(isTaskRun)
            .find((entry) => entry.itemId === task.itemId);
          if (run) {
            this.childTimer.afterCompletionChange(run);
          }
        }

        // The backend awards/revokes a star as part of the same request that just completed above
        // (see SetTaskCompletionHandler), so re-reading progress now already reflects it -- no
        // local point math to duplicate or get out of sync with milestone thresholds.
        this.progress.reload();
      },
      'child.home.loadError',
    );
  }

  // A disabled feature's section loads empty, so it isn't shown and its 404 isn't a load error.
  private async loadTodaysPickups(): Promise<PickupOccurrence[]> {
    if (!this.features.enabled('pickups')) {
      return [];
    }

    try {
      const me = await this.users.ensureCurrentUser();
      const today = this.clock.today();
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
    if (!this.features.enabled('mealplans')) {
      return {};
    }

    const entries = await this.mealplans.listMealPlan({ kind: 'family', childId }, today, today);
    const bySlot: Partial<Record<MealSlot, MealPlanEntry>> = {};

    for (const entry of entries) {
      bySlot[entry.slot] = entry;
    }

    return bySlot;
  }

  private async loadDoses(childId: string, today: string): Promise<MedicineDoseOccurrence[]> {
    if (!this.features.enabled('medicines')) {
      return [];
    }

    const occurrences = await this.medicines.listDoses(childId, today, today);
    return [...occurrences].sort((a, b) => a.time.localeCompare(b.time));
  }

  private async loadCalendarOccurrences(): Promise<TodaysOccurrences> {
    const occurrences = await this.calendars.listTodayOccurrences(this.clock.today());

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
