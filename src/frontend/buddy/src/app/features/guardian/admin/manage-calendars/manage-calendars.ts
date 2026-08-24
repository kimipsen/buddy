import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarSummary, CalendarsService } from '../../../../core/calendars.service';
import { browserTimeZoneId, listTimeZoneIds } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

const ROLE_LABELS: Record<number, string> = {
  0: 'admin.manageCalendars.roles.owner',
  1: 'admin.manageCalendars.roles.contributor',
  2: 'admin.manageCalendars.roles.viewer'
};

@Component({
  selector: 'app-manage-calendars',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-calendars.html'
})
export class ManageCalendars implements OnInit {
  private readonly calendars = inject(CalendarsService);
  private readonly groupsService = inject(GroupsService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly timeZoneIds = listTimeZoneIds();

  protected readonly items = signal<CalendarSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newCalendarName = signal('');
  protected readonly newCalendarTimeZoneId = signal(browserTimeZoneId());
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  // A calendar is always group-owned -- this stays empty (and the create form disabled) until
  // a manageable group is loaded and selected below.
  protected readonly newCalendarGroupId = signal('');
  protected readonly manageableGroups = signal<GroupSummary[]>([]);

  ngOnInit(): void {
    void this.loadCalendars();
    void this.loadManageableGroups();
  }

  protected async createCalendar(): Promise<void> {
    const name = this.newCalendarName().trim();
    const timeZoneId = this.newCalendarTimeZoneId().trim();
    const groupId = this.newCalendarGroupId();

    if (!name || !timeZoneId || !groupId) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.calendars.createCalendar({ name, timeZoneId, groupId });
      this.newCalendarName.set('');
      await this.loadCalendars();
    } catch {
      this.createError.set('admin.manageCalendars.createError');
    } finally {
      this.creating.set(false);
    }
  }

  private async loadManageableGroups(): Promise<void> {
    try {
      const groups = await this.groupsService.listMyGroups();
      // Group-owned calendar creation is gated on GroupAuthorization.CheckManage server-side,
      // which only Owners (0) and Admins (1) satisfy.
      const manageable = groups.filter((group) => group.role === 0 || group.role === 1);
      this.manageableGroups.set(manageable);

      if (!this.newCalendarGroupId() && manageable.length > 0) {
        this.newCalendarGroupId.set(manageable[0].id);
      }
    } catch {
      this.manageableGroups.set([]);
    }
  }

  private async loadCalendars(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.items.set(await this.calendars.listMyCalendars());
    } catch {
      this.error.set('admin.manageCalendars.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}
