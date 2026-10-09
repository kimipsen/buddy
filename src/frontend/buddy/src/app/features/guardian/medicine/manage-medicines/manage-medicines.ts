import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { todayIsoDate } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MedicineSchedule, MedicinesService } from '../../../../core/medicines.service';
import { ActionState, createAction } from '../../../../shared/action-state/action-state';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { RepeatableRow } from '../../../../shared/repeatable-row/repeatable-row';
import { TimeSelect } from '../../../../shared/time-select/time-select';

const DEFAULT_COLOR = '#f43f5e';

interface SharedGroup {
  groupId: string;
  groupName: string;
}

// What the page shows for the selected child: its active schedules and its group-sharing state.
interface ChildMedicines {
  schedules: MedicineSchedule[];
  manageableGroups: GroupSummary[];
  sharedGroup: SharedGroup | null;
}

const EMPTY_CHILD_MEDICINES: ChildMedicines = {
  schedules: [],
  manageableGroups: [],
  sharedGroup: null,
};

// An action's state as the selected child sees it: another child's run shows as idle. Ids are the
// child id, or `${childId}|${scheduleId}` for a stop.
function stateForChild(
  state: ActionState<string>,
  childId: string | undefined,
): ActionState<string> {
  return state.status === 'idle' || state.id === childId || state.id.startsWith(`${childId}|`)
    ? state
    : { status: 'idle' };
}

function withSeconds(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}

function withoutSeconds(time: string): string {
  return time.slice(0, 5);
}

@Component({
  selector: 'app-manage-medicines',
  imports: [FormsModule, TranslatePipe, ColorSwatchPicker, RepeatableRow, TimeSelect],
  templateUrl: './manage-medicines.html',
})
export class ManageMedicines {
  private readonly guardians = inject(GuardiansService);
  private readonly medicines = inject(MedicinesService);
  private readonly groupsService = inject(GroupsService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  // The first child until the guardian picks another.
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  protected readonly childMedicines = resource({
    params: () => this.selectedChildId(),
    loader: ({ params: childId }) => this.loadChildMedicines(childId),
  });
  // Empty while loading or after a failed load, so the page still renders.
  protected readonly loaded = computed((): ChildMedicines =>
    this.childMedicines.hasValue() ? this.childMedicines.value() : EMPTY_CHILD_MEDICINES,
  );

  protected readonly newName = signal('');
  protected readonly newDosage = signal('');
  protected readonly newIcon = signal('💊');
  protected readonly newColor = signal(DEFAULT_COLOR);
  protected readonly newTimes = signal<string[]>(['08:00']);
  protected readonly newStartDate = signal(todayIsoDate());
  protected readonly newEndDate = signal('');
  protected readonly creating = createAction<string>();
  protected readonly createState = computed(() =>
    stateForChild(this.creating.state(), this.selectedChildId()),
  );

  protected readonly stopping = createAction<string>();
  protected readonly stopState = computed(() =>
    stateForChild(this.stopping.state(), this.selectedChildId()),
  );
  protected readonly confirmingStopScheduleId = signal<string | null>(null);

  protected readonly shareTargetGroupId = signal('');
  protected readonly sharing = createAction<string>();
  protected readonly shareState = computed(() =>
    stateForChild(this.sharing.state(), this.selectedChildId()),
  );

  protected async shareWithGroup(): Promise<void> {
    const childId = this.selectedChildId();
    const groupId = this.shareTargetGroupId();
    const groupName = this.loaded().manageableGroups.find((group) => group.id === groupId)?.name;

    if (!childId || !groupId || !groupName) {
      return;
    }

    await this.sharing.run(
      childId,
      async () => {
        await this.medicines.shareWithGroup(childId, groupId);
        this.updateChildMedicines(childId, { sharedGroup: { groupId, groupName } });
        this.shareTargetGroupId.set('');
      },
      'medicine.manageMedicines.sharing.shareError',
    );
  }

  protected async unshareFromGroup(): Promise<void> {
    const childId = this.selectedChildId();
    const groupId = this.loaded().sharedGroup?.groupId;

    if (!childId || !groupId) {
      return;
    }

    await this.sharing.run(
      childId,
      async () => {
        await this.medicines.unshareFromGroup(childId, groupId);
        this.updateChildMedicines(childId, { sharedGroup: null });
      },
      'medicine.manageMedicines.sharing.unshareError',
    );
  }

  protected addTimeField(): void {
    this.newTimes.update((times) => [...times, '08:00']);
  }

  protected removeTimeField(index: number): void {
    this.newTimes.update((times) => times.filter((_, i) => i !== index));
  }

  protected setTimeField(index: number, value: string): void {
    this.newTimes.update((times) => times.map((time, i) => (i === index ? value : time)));
  }

  protected async createSchedule(): Promise<void> {
    const childId = this.selectedChildId();
    const name = this.newName().trim();
    const dosage = this.newDosage().trim();
    const icon = this.newIcon().trim();
    const color = this.newColor().trim();
    const times = this.newTimes().filter((time) => time.trim());
    const startDate = this.newStartDate().trim();

    if (!childId || !name || !dosage || !icon || !color || times.length === 0 || !startDate) {
      return;
    }

    await this.creating.run(
      childId,
      async () => {
        await this.medicines.createSchedule(childId, {
          name,
          dosage,
          icon,
          color,
          times: times.map(withSeconds),
          startDate,
          endDate: this.newEndDate().trim() || null,
        });
        this.newName.set('');
        this.newDosage.set('');
        this.newIcon.set('💊');
        this.newColor.set(DEFAULT_COLOR);
        this.newTimes.set(['08:00']);
        this.newStartDate.set(todayIsoDate());
        this.newEndDate.set('');
        this.updateChildMedicines(childId, { schedules: await this.loadSchedules(childId) });
      },
      'medicine.manageMedicines.form.createError',
    );
  }

  protected stopId(scheduleId: string): string {
    return `${this.selectedChildId()}|${scheduleId}`;
  }

  protected requestStop(scheduleId: string): void {
    this.stopping.clearError();
    this.confirmingStopScheduleId.set(scheduleId);
  }

  protected cancelStop(): void {
    this.confirmingStopScheduleId.set(null);
    this.stopping.clearError();
  }

  protected async confirmStop(scheduleId: string): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    await this.stopping.run(
      `${childId}|${scheduleId}`,
      async () => {
        await this.medicines.stopSchedule(childId, scheduleId);
        this.confirmingStopScheduleId.set(null);
        this.updateChildMedicines(childId, { schedules: await this.loadSchedules(childId) });
      },
      'medicine.manageMedicines.stopError',
    );
  }

  protected formatTime(time: string): string {
    return withoutSeconds(time);
  }

  // Applies a mutation's result only if the page still shows the child it was made for, and that
  // child's data has loaded: setting the resource mid-load would cancel the load.
  private updateChildMedicines(childId: string, change: Partial<ChildMedicines>): void {
    const childMedicines = this.childMedicines;

    if (
      this.selectedChildId() === childId &&
      childMedicines.hasValue() &&
      !childMedicines.isLoading()
    ) {
      childMedicines.set({ ...childMedicines.value(), ...change });
    }
  }

  private async loadChildMedicines(childId: string): Promise<ChildMedicines> {
    const schedules = await this.loadSchedules(childId);
    return { schedules, ...(await this.loadSharing(childId)) };
  }

  private async loadSchedules(childId: string): Promise<MedicineSchedule[]> {
    return (await this.medicines.listSchedules(childId)).filter((schedule) => !schedule.isStopped);
  }

  // Best-effort: a failure shows the page as not shared, with no groups to share with.
  private async loadSharing(
    childId: string,
  ): Promise<Pick<ChildMedicines, 'manageableGroups' | 'sharedGroup'>> {
    try {
      const [groups, sharedGroup] = await Promise.all([
        this.groupsService.listMyGroups(),
        this.medicines.getSharedGroup(childId),
      ]);

      // Only Owner/Admin can share/unshare (GroupAuthorization.CheckManage), matching the
      // backend's two-sided consent for ShareMedicineWithGroup.
      return {
        manageableGroups: groups.filter((g) => g.role === 'Owner' || g.role === 'Admin'),
        sharedGroup,
      };
    } catch {
      return { manageableGroups: [], sharedGroup: null };
    }
  }
}
