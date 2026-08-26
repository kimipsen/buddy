import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth.service';
import { CalendarItemKind, CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService, SiblingSummary } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { DoseStatus, MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { PickupAssigneeKind, PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { ProgressService, ProgressSummary } from '../../../core/progress.service';
import { UserDatePipe } from '../../../core/user-date.pipe';
import { UsersService } from '../../../core/users.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ProgressBadge } from '../../../shared/progress-badge/progress-badge';

const EVENT_KIND: CalendarItemKind = 0;
const TASK_KIND: CalendarItemKind = 1;

const PENDING: DoseStatus = 0;
const TAKEN: DoseStatus = 1;
const SKIPPED: DoseStatus = 2;

const MEAL_SLOT_LABELS: Record<MealSlot, string> = {
  0: 'dashboard.mealplan.slots.breakfast',
  1: 'dashboard.mealplan.slots.lunch',
  2: 'dashboard.mealplan.slots.dinner',
  3: 'dashboard.mealplan.slots.snack'
};

const MEAL_SLOTS: MealSlot[] = [0, 1, 2, 3];
const STARS = [1, 2, 3, 4, 5];

const GUARDIAN: PickupAssigneeKind = 0;
const SELF_ESCORT: PickupAssigneeKind = 1;
const SIBLING: PickupAssigneeKind = 2;
const PLAYDATE: PickupAssigneeKind = 3;

const PICKUP_SLOT_LABELS = { 0: 'child.home.pickup.slots.dropOff', 1: 'child.home.pickup.slots.pickUp' } as const;

@Component({
  selector: 'app-child-home',
  imports: [TranslatePipe, RouterLink, LoadingSpinner, ProgressBadge, UserDatePipe],
  templateUrl: './home.html'
})
export class ChildHome implements OnInit {
  private readonly auth = inject(AuthService);
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
  protected readonly pickupSlotLabels = PICKUP_SLOT_LABELS;

  protected readonly guardianList = signal<GuardianSummary[]>([]);
  protected readonly siblingList = signal<SiblingSummary[]>([]);
  protected readonly todaysPickups = signal<PickupOccurrence[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Pickups, meals, doses, and tasks all feed hasAnything() -- until every one of them has
  // loaded, a light day and a still-loading day would render the same empty-state card.
  protected readonly contentLoading = signal(true);

  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;
  protected readonly stars = STARS;
  protected readonly entriesBySlot = signal<Partial<Record<MealSlot, MealPlanEntry>>>({});
  // Only meals actually planned today, in slot order -- unlike the guardian widget, this skips
  // "not planned" filler rows entirely (see the "if any" layout decision in
  // docs/frontend/analysis/child-day-dashboard.md).
  protected readonly mealsToShow = computed(() => MEAL_SLOTS.map((slot) => this.entriesBySlot()[slot]).filter((entry) => entry !== undefined));

  // Rating today's meals right away (rather than only from the past-weeks planner) so the child
  // doesn't have to remember how a meal was by the time they'd next see it there.
  protected readonly savingSlot = signal<MealSlot | null>(null);
  protected readonly editingSlot = signal<MealSlot | null>(null);
  protected readonly commentDraft = signal('');
  private childId: string | null = null;

  protected readonly pending = PENDING;
  protected readonly taken = TAKEN;
  protected readonly skipped = SKIPPED;
  protected readonly doses = signal<MedicineDoseOccurrence[]>([]);
  protected readonly savingDoseKey = signal<string | null>(null);

  protected readonly tasks = signal<CalendarOccurrence[]>([]);
  protected readonly savingTaskId = signal<string | null>(null);

  protected readonly events = signal<CalendarOccurrence[]>([]);

  protected readonly progress = signal<ProgressSummary>({ totalStars: 0, unlockedMilestones: [] });

  // Only when every section is empty do we show the "nothing to show yet" card -- a light day
  // shouldn't render five empty-state messages back to back.
  protected readonly hasAnything = computed(
    () =>
      this.todaysPickups().length > 0 ||
      this.mealsToShow().length > 0 ||
      this.doses().length > 0 ||
      this.tasks().length > 0 ||
      this.events().length > 0
  );

  ngOnInit(): void {
    void this.loadGuardians();
    void this.loadSiblings();
    void this.loadProgress();
    void Promise.all([this.loadTodaysPickups(), this.loadDashboard()]).finally(() => this.contentLoading.set(false));
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected assigneeName(occurrence: PickupOccurrence): string | null {
    if (occurrence.kind === this.guardianKind) {
      return this.guardianList().find((guardian) => guardian.id === occurrence.guardianId)?.name.givenName ?? null;
    }

    if (occurrence.kind === this.siblingKind) {
      return this.siblingList().find((sibling) => sibling.id === occurrence.siblingChildId)?.name.givenName ?? null;
    }

    return null;
  }

  protected doseKey(dose: MedicineDoseOccurrence): string {
    return `${dose.medicineId}|${dose.time}`;
  }

  protected async setDoseStatus(dose: MedicineDoseOccurrence, status: DoseStatus): Promise<void> {
    const key = this.doseKey(dose);
    this.savingDoseKey.set(key);

    try {
      const me = await this.users.ensureCurrentUser();
      const updated = await this.medicines.setDoseStatus(me.id, dose.medicineId, dose.date, dose.time, status);
      this.doses.update((current) => current.map((existing) => (this.doseKey(existing) === key ? { ...existing, status: updated.status } : existing)));
    } catch {
      this.error.set('child.home.loadError');
    } finally {
      this.savingDoseKey.set(null);
    }
  }

  protected async toggleTask(task: CalendarOccurrence): Promise<void> {
    this.savingTaskId.set(task.itemId);
    const isCompleted = !task.isCompleted;

    try {
      await this.calendars.setTaskCompletion(task.calendarId, task.itemId, todayIsoDate(), isCompleted);
      this.tasks.update((current) => current.map((existing) => (existing.itemId === task.itemId ? { ...existing, isCompleted } : existing)));

      // The backend awards/revokes a star as part of the same request that just completed above
      // (see SetTaskCompletionHandler), so re-reading progress now already reflects it -- no
      // local point math to duplicate or get out of sync with milestone thresholds.
      void this.loadProgress();
    } catch {
      this.error.set('child.home.loadError');
    } finally {
      this.savingTaskId.set(null);
    }
  }

  private async loadProgress(): Promise<void> {
    try {
      this.progress.set(await this.progressService.getMyProgress());
    } catch {
      // Progress is a supplementary widget, not core dashboard data -- a failed load just means
      // no badge shows, the same non-blocking treatment as loadSiblings().
    }
  }

  private async loadGuardians(): Promise<void> {
    this.loading.set(true);

    try {
      this.guardianList.set(await this.guardians.listMyGuardians());
    } finally {
      this.loading.set(false);
    }
  }

  private async loadSiblings(): Promise<void> {
    try {
      this.siblingList.set(await this.guardians.listMySiblings());
    } catch {
      this.siblingList.set([]);
    }
  }

  private async loadTodaysPickups(): Promise<void> {
    try {
      const me = await this.users.ensureCurrentUser();
      const today = todayIsoDate();
      this.todaysPickups.set(await this.pickups.listSchedule(me.id, today, today));
    } catch {
      this.todaysPickups.set([]);
    }
  }

  private async loadDashboard(): Promise<void> {
    try {
      const me = await this.users.ensureCurrentUser();
      this.childId = me.id;
      const today = todayIsoDate();

      await Promise.all([this.loadMeals(me.id, today), this.loadDoses(me.id, today), this.loadCalendarOccurrences()]);
    } catch {
      this.error.set('child.home.loadError');
    }
  }

  protected isEditing(entry: MealPlanEntry): boolean {
    return this.editingSlot() === entry.slot;
  }

  protected startEditing(entry: MealPlanEntry): void {
    this.editingSlot.set(entry.slot);
    this.commentDraft.set(entry.rating?.comment ?? '');
  }

  protected cancelEditing(): void {
    this.editingSlot.set(null);
    this.commentDraft.set('');
  }

  protected setComment(value: string): void {
    this.commentDraft.set(value);
  }

  // Tapping a star rates immediately with whatever comment is already on file -- a quick
  // reaction shouldn't require opening the comment form first.
  protected async rate(entry: MealPlanEntry, starCount: number): Promise<void> {
    await this.submitRating(entry, starCount, entry.rating?.comment ?? null);
  }

  protected async saveComment(entry: MealPlanEntry): Promise<void> {
    const starCount = entry.rating?.stars ?? STARS[STARS.length - 1];
    await this.submitRating(entry, starCount, this.commentDraft().trim() || null);
    this.cancelEditing();
  }

  private async submitRating(entry: MealPlanEntry, starCount: number, comment: string | null): Promise<void> {
    if (!this.childId) {
      return;
    }

    this.savingSlot.set(entry.slot);
    this.error.set(null);

    try {
      const meal = await this.mealplans.rateMeal(this.childId, entry.mealId, starCount, comment);
      const myRating = meal.ratings.find((rating) => rating.childId === this.childId) ?? null;

      this.entriesBySlot.update((current) => {
        const next = { ...current };

        for (const [slot, existing] of Object.entries(next)) {
          if (existing?.mealId === entry.mealId) {
            next[Number(slot) as MealSlot] = { ...existing, rating: myRating };
          }
        }

        return next;
      });
    } catch {
      this.error.set('child.mealplan.rateError');
    } finally {
      this.savingSlot.set(null);
    }
  }

  private async loadMeals(childId: string, today: string): Promise<void> {
    const entries = await this.mealplans.listMealPlan({ kind: 'family', childId }, today, today);
    const bySlot: Partial<Record<MealSlot, MealPlanEntry>> = {};

    for (const entry of entries) {
      bySlot[entry.slot] = entry;
    }

    this.entriesBySlot.set(bySlot);
  }

  private async loadDoses(childId: string, today: string): Promise<void> {
    const occurrences = await this.medicines.listDoses(childId, today, today);
    this.doses.set([...occurrences].sort((a, b) => a.time.localeCompare(b.time)));
  }

  private async loadCalendarOccurrences(): Promise<void> {
    const occurrences = await this.calendars.listTodayOccurrences();

    const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);

    tasks.sort((a, b) => {
      if (a.dueAt === null) {
        return b.dueAt === null ? 0 : 1;
      }

      return b.dueAt === null ? -1 : a.dueAt.localeCompare(b.dueAt);
    });

    this.tasks.set(tasks);

    const events = occurrences.filter((occurrence) => occurrence.kind === EVENT_KIND);

    events.sort((a, b) => {
      if (a.startsAt === null) {
        return b.startsAt === null ? 0 : 1;
      }

      return b.startsAt === null ? -1 : a.startsAt.localeCompare(b.startsAt);
    });

    this.events.set(events);
  }
}
