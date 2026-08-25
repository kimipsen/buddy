import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { AuthService } from '../../../core/auth.service';
import { CalendarItemKind, CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MealPlanEntry, MealSlot, MealplansService } from '../../../core/mealplans.service';
import { DoseStatus, MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { PickupAssigneeKind, PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { UsersService } from '../../../core/users.service';

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

const GUARDIAN: PickupAssigneeKind = 0;
const SELF_ESCORT: PickupAssigneeKind = 1;
const SIBLING: PickupAssigneeKind = 2;
const PLAYDATE: PickupAssigneeKind = 3;

const PICKUP_SLOT_LABELS = { 0: 'child.home.pickup.slots.dropOff', 1: 'child.home.pickup.slots.pickUp' } as const;

@Component({
  selector: 'app-child-home',
  imports: [TranslatePipe],
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

  protected readonly guardianKind = GUARDIAN;
  protected readonly selfEscortKind = SELF_ESCORT;
  protected readonly siblingKind = SIBLING;
  protected readonly playdateKind = PLAYDATE;
  protected readonly pickupSlotLabels = PICKUP_SLOT_LABELS;

  protected readonly guardianList = signal<GuardianSummary[]>([]);
  protected readonly todaysPickups = signal<PickupOccurrence[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;
  protected readonly entriesBySlot = signal<Partial<Record<MealSlot, MealPlanEntry>>>({});
  // Only meals actually planned today, in slot order -- unlike the guardian widget, this skips
  // "not planned" filler rows entirely (see the "if any" layout decision in
  // docs/frontend/analysis/child-day-dashboard.md).
  protected readonly mealsToShow = computed(() => MEAL_SLOTS.map((slot) => this.entriesBySlot()[slot]).filter((entry) => entry !== undefined));

  protected readonly pending = PENDING;
  protected readonly taken = TAKEN;
  protected readonly skipped = SKIPPED;
  protected readonly doses = signal<MedicineDoseOccurrence[]>([]);
  protected readonly savingDoseKey = signal<string | null>(null);

  protected readonly tasks = signal<CalendarOccurrence[]>([]);
  protected readonly savingTaskId = signal<string | null>(null);

  // Only when every section is empty do we show the "nothing to show yet" card -- a light day
  // shouldn't render four empty-state messages back to back.
  protected readonly hasAnything = computed(
    () => this.todaysPickups().length > 0 || this.mealsToShow().length > 0 || this.doses().length > 0 || this.tasks().length > 0
  );

  ngOnInit(): void {
    void this.loadGuardians();
    void this.loadTodaysPickups();
    void this.loadDashboard();
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected assigneeName(occurrence: PickupOccurrence): string | null {
    if (occurrence.kind === this.guardianKind) {
      return this.guardianList().find((guardian) => guardian.id === occurrence.guardianId)?.name.givenName ?? null;
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
    } catch {
      this.error.set('child.home.loadError');
    } finally {
      this.savingTaskId.set(null);
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
      const today = todayIsoDate();

      await Promise.all([this.loadMeals(me.id, today), this.loadDoses(me.id, today), this.loadTasks()]);
    } catch {
      this.error.set('child.home.loadError');
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

  private async loadTasks(): Promise<void> {
    const occurrences = await this.calendars.listTodayOccurrences();
    const tasks = occurrences.filter((occurrence) => occurrence.kind === TASK_KIND);

    tasks.sort((a, b) => {
      if (a.dueAt === null) {
        return b.dueAt === null ? 0 : 1;
      }

      return b.dueAt === null ? -1 : a.dueAt.localeCompare(b.dueAt);
    });

    this.tasks.set(tasks);
  }
}
