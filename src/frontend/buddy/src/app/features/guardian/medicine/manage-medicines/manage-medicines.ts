import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { todayIsoDate } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MedicineSchedule, MedicinesService } from '../../../../core/medicines.service';
import { TimeSelect } from '../../../../shared/time-select/time-select';

const DEFAULT_COLOR = '#f43f5e';

function withSeconds(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}

function withoutSeconds(time: string): string {
  return time.slice(0, 5);
}

@Component({
  selector: 'app-manage-medicines',
  imports: [FormsModule, TranslatePipe, TimeSelect],
  templateUrl: './manage-medicines.html'
})
export class ManageMedicines implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly medicines = inject(MedicinesService);
  private readonly groupsService = inject(GroupsService);

  protected readonly hasChildren = signal(true);
  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);

  protected readonly schedules = signal<MedicineSchedule[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newName = signal('');
  protected readonly newDosage = signal('');
  protected readonly newIcon = signal('💊');
  protected readonly newColor = signal(DEFAULT_COLOR);
  protected readonly newTimes = signal<string[]>(['08:00']);
  protected readonly newStartDate = signal(todayIsoDate());
  protected readonly newEndDate = signal('');
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly stoppingScheduleId = signal<string | null>(null);
  protected readonly confirmingStopScheduleId = signal<string | null>(null);

  protected readonly manageableGroups = signal<GroupSummary[]>([]);
  protected readonly sharedGroupId = signal<string | null>(null);
  protected readonly sharedGroupName = signal<string | null>(null);
  protected readonly shareTargetGroupId = signal('');
  protected readonly sharing = signal(false);
  protected readonly shareError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected async onChildChange(childId: string): Promise<void> {
    this.selectedChildId.set(childId);
    await this.loadSchedules(childId);
    await this.loadSharing(childId);
  }

  protected async shareWithGroup(): Promise<void> {
    const childId = this.selectedChildId();
    const groupId = this.shareTargetGroupId();
    const groupName = this.manageableGroups().find((group) => group.id === groupId)?.name;

    if (!childId || !groupId || !groupName) {
      return;
    }

    this.sharing.set(true);
    this.shareError.set(null);

    try {
      await this.medicines.shareWithGroup(childId, groupId);
      this.sharedGroupId.set(groupId);
      this.sharedGroupName.set(groupName);
      this.shareTargetGroupId.set('');
    } catch {
      this.shareError.set('medicine.manageMedicines.sharing.shareError');
    } finally {
      this.sharing.set(false);
    }
  }

  protected async unshareFromGroup(): Promise<void> {
    const childId = this.selectedChildId();
    const groupId = this.sharedGroupId();

    if (!childId || !groupId) {
      return;
    }

    this.sharing.set(true);
    this.shareError.set(null);

    try {
      await this.medicines.unshareFromGroup(childId, groupId);
      this.sharedGroupId.set(null);
      this.sharedGroupName.set(null);
    } catch {
      this.shareError.set('medicine.manageMedicines.sharing.unshareError');
    } finally {
      this.sharing.set(false);
    }
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

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.medicines.createSchedule(childId, {
        name,
        dosage,
        icon,
        color,
        times: times.map(withSeconds),
        startDate,
        endDate: this.newEndDate().trim() || null
      });
      this.newName.set('');
      this.newDosage.set('');
      this.newIcon.set('💊');
      this.newColor.set(DEFAULT_COLOR);
      this.newTimes.set(['08:00']);
      this.newStartDate.set(todayIsoDate());
      this.newEndDate.set('');
      await this.loadSchedules(childId);
    } catch {
      this.createError.set('medicine.manageMedicines.form.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected requestStop(scheduleId: string): void {
    this.error.set(null);
    this.confirmingStopScheduleId.set(scheduleId);
  }

  protected cancelStop(): void {
    this.confirmingStopScheduleId.set(null);
  }

  protected async confirmStop(scheduleId: string): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    this.stoppingScheduleId.set(scheduleId);
    this.error.set(null);

    try {
      await this.medicines.stopSchedule(childId, scheduleId);
      this.confirmingStopScheduleId.set(null);
      await this.loadSchedules(childId);
    } catch {
      this.error.set('medicine.manageMedicines.stopError');
    } finally {
      this.stoppingScheduleId.set(null);
    }
  }

  protected formatTime(time: string): string {
    return withoutSeconds(time);
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.children.set(children);
      this.selectedChildId.set(children[0].id);
      await this.loadSchedules(children[0].id);
      await this.loadSharing(children[0].id);
    } catch {
      this.error.set('medicine.manageMedicines.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadSchedules(childId: string): Promise<void> {
    this.schedules.set((await this.medicines.listSchedules(childId)).filter((schedule) => !schedule.isStopped));
  }

  private async loadSharing(childId: string): Promise<void> {
    try {
      const [groups, sharedGroup] = await Promise.all([this.groupsService.listMyGroups(), this.medicines.getSharedGroup(childId)]);

      // Only Owner/Admin can share/unshare (GroupAuthorization.CheckManage), matching the
      // backend's two-sided consent for ShareMedicineWithGroup.
      this.manageableGroups.set(groups.filter((g) => g.role === 0 || g.role === 1));
      this.sharedGroupId.set(sharedGroup?.groupId ?? null);
      this.sharedGroupName.set(sharedGroup?.groupName ?? null);
    } catch {
      this.manageableGroups.set([]);
      this.sharedGroupId.set(null);
      this.sharedGroupName.set(null);
    }
  }
}
