import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarSummary, CalendarsService, IcalTokenSummary } from '../../../../core/calendars.service';
import { browserTimeZoneId, listTimeZoneIds } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

const ROLE_LABELS: Record<number, string> = {
  0: 'admin.manageCalendars.roles.owner',
  1: 'admin.manageCalendars.roles.contributor',
  2: 'admin.manageCalendars.roles.viewer'
};

// Matches the backend's Calendar.DefaultIcon -- what a new calendar gets if this field is left as-is.
const DEFAULT_ICON = '📅';

// browserTimeZoneId() (Intl.DateTimeFormat().resolvedOptions().timeZone) can resolve to an alias
// like "UTC" that Intl.supportedValuesOf('timeZone') -- and so `timeZoneIds` -- does not include
// (it only lists canonical IANA names like "Etc/UTC"). Falling back to the first listed zone keeps
// the pre-selected value one the <select> actually has an <option> for, rather than silently
// submitting a value the guardian never saw selected.
function resolveDefaultTimeZoneId(candidates: readonly string[]): string {
  const browserZone = browserTimeZoneId();
  return candidates.includes(browserZone) ? browserZone : (candidates[0] ?? browserZone);
}

@Component({
  selector: 'app-manage-calendars',
  imports: [FormsModule, DatePipe, TranslatePipe],
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
  protected readonly newCalendarIcon = signal(DEFAULT_ICON);
  protected readonly newCalendarTimeZoneId = signal(resolveDefaultTimeZoneId(this.timeZoneIds));
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  // A calendar is always group-owned -- this stays empty (and the create form disabled) until
  // a manageable group is loaded and selected below.
  protected readonly newCalendarGroupId = signal('');
  protected readonly manageableGroups = signal<GroupSummary[]>([]);

  protected readonly movingCalendarId = signal<string | null>(null);
  protected readonly moveTargetGroupId = signal('');
  protected readonly moving = signal(false);
  protected readonly moveError = signal<string | null>(null);

  protected readonly editingIconCalendarId = signal<string | null>(null);
  protected readonly editIconValue = signal('');
  protected readonly updatingIcon = signal(false);
  protected readonly editIconError = signal<string | null>(null);

  protected readonly confirmingDeleteCalendarId = signal<string | null>(null);
  protected readonly deletingCalendarId = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);

  protected readonly icalCalendarId = signal<string | null>(null);
  protected readonly icalTokens = signal<IcalTokenSummary[]>([]);
  protected readonly icalLoading = signal(false);
  protected readonly icalError = signal<string | null>(null);
  protected readonly icalCreating = signal(false);
  protected readonly icalCreateError = signal<string | null>(null);
  protected readonly icalRevokingTokenId = signal<string | null>(null);
  // The plaintext URL is only ever available right after creation -- once this panel closes or a
  // new token is issued, it's gone from the client just like it's gone from the server.
  protected readonly newIcalUrl = signal<string | null>(null);
  protected readonly icalCopied = signal(false);

  ngOnInit(): void {
    void this.loadCalendars();
    void this.loadManageableGroups();
  }

  protected async createCalendar(): Promise<void> {
    const name = this.newCalendarName().trim();
    const timeZoneId = this.newCalendarTimeZoneId().trim();
    const groupId = this.newCalendarGroupId();
    const icon = this.newCalendarIcon().trim() || null;

    if (!name || !timeZoneId || !groupId) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      await this.calendars.createCalendar({ name, timeZoneId, groupId, icon });
      this.newCalendarName.set('');
      this.newCalendarIcon.set(DEFAULT_ICON);
      await this.loadCalendars();
    } catch {
      this.createError.set('admin.manageCalendars.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected startMove(calendarId: string): void {
    this.confirmingDeleteCalendarId.set(null);
    this.icalCalendarId.set(null);
    this.editingIconCalendarId.set(null);

    if (this.movingCalendarId() === calendarId) {
      this.movingCalendarId.set(null);
      return;
    }

    this.movingCalendarId.set(calendarId);
    this.moveTargetGroupId.set('');
    this.moveError.set(null);
  }

  protected startEditIcon(calendar: CalendarSummary): void {
    this.movingCalendarId.set(null);
    this.confirmingDeleteCalendarId.set(null);
    this.icalCalendarId.set(null);

    if (this.editingIconCalendarId() === calendar.id) {
      this.editingIconCalendarId.set(null);
      return;
    }

    this.editingIconCalendarId.set(calendar.id);
    this.editIconValue.set(calendar.icon);
    this.editIconError.set(null);
  }

  protected cancelEditIcon(): void {
    this.editingIconCalendarId.set(null);
  }

  protected async confirmEditIcon(calendarId: string): Promise<void> {
    const icon = this.editIconValue().trim();

    if (!icon) {
      return;
    }

    this.updatingIcon.set(true);
    this.editIconError.set(null);

    try {
      await this.calendars.updateCalendarIcon(calendarId, icon);
      this.editingIconCalendarId.set(null);
      await this.loadCalendars();
    } catch {
      this.editIconError.set('admin.manageCalendars.editIcon.error');
    } finally {
      this.updatingIcon.set(false);
    }
  }

  protected async confirmMove(calendarId: string): Promise<void> {
    const groupId = this.moveTargetGroupId();

    if (!groupId) {
      return;
    }

    this.moving.set(true);
    this.moveError.set(null);

    try {
      await this.calendars.transferToGroup(calendarId, groupId);
      this.movingCalendarId.set(null);
      await this.loadCalendars();
    } catch {
      this.moveError.set('admin.manageCalendars.move.error');
    } finally {
      this.moving.set(false);
    }
  }

  protected requestDelete(calendarId: string): void {
    this.movingCalendarId.set(null);
    this.icalCalendarId.set(null);
    this.editingIconCalendarId.set(null);
    this.deleteError.set(null);
    this.confirmingDeleteCalendarId.set(calendarId);
  }

  protected cancelDelete(): void {
    this.confirmingDeleteCalendarId.set(null);
  }

  protected async confirmDelete(calendarId: string): Promise<void> {
    this.deletingCalendarId.set(calendarId);
    this.deleteError.set(null);

    try {
      await this.calendars.deleteCalendar(calendarId);
      this.confirmingDeleteCalendarId.set(null);
      await this.loadCalendars();
    } catch {
      this.deleteError.set('admin.manageCalendars.delete.error');
    } finally {
      this.deletingCalendarId.set(null);
    }
  }

  protected toggleIcal(calendarId: string): void {
    this.movingCalendarId.set(null);
    this.confirmingDeleteCalendarId.set(null);
    this.editingIconCalendarId.set(null);

    if (this.icalCalendarId() === calendarId) {
      this.icalCalendarId.set(null);
      return;
    }

    this.icalCalendarId.set(calendarId);
    this.newIcalUrl.set(null);
    this.icalCreateError.set(null);
    void this.loadIcalTokens(calendarId);
  }

  protected async createIcalToken(calendarId: string): Promise<void> {
    this.icalCreating.set(true);
    this.icalCreateError.set(null);
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);

    try {
      const issued = await this.calendars.createIcalToken(calendarId);
      this.newIcalUrl.set(this.calendars.icalFeedUrl(issued.subscriptionPath));
      await this.loadIcalTokens(calendarId);
    } catch {
      this.icalCreateError.set('admin.manageCalendars.ical.createError');
    } finally {
      this.icalCreating.set(false);
    }
  }

  protected async revokeIcalToken(calendarId: string, tokenId: string): Promise<void> {
    this.icalRevokingTokenId.set(tokenId);
    this.icalError.set(null);

    try {
      await this.calendars.revokeIcalToken(calendarId, tokenId);
      await this.loadIcalTokens(calendarId);
    } catch {
      this.icalError.set('admin.manageCalendars.ical.revokeError');
    } finally {
      this.icalRevokingTokenId.set(null);
    }
  }

  protected async copyIcalUrl(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.icalCopied.set(true);
    } catch {
      this.icalCopied.set(false);
    }
  }

  private async loadIcalTokens(calendarId: string): Promise<void> {
    this.icalLoading.set(true);
    this.icalError.set(null);

    try {
      this.icalTokens.set(await this.calendars.listIcalTokens(calendarId));
    } catch {
      this.icalError.set('admin.manageCalendars.ical.loadError');
    } finally {
      this.icalLoading.set(false);
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
