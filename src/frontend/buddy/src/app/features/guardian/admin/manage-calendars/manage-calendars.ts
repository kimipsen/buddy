import { DatePipe } from '@angular/common';
import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  CalendarRole,
  CalendarSummary,
  CalendarsService,
} from '../../../../core/calendars.service';
import { browserTimeZoneId, listTimeZoneIds } from '../../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { createAction } from '../../../../shared/action-state/action-state';
import { IcalSubscribeLinks } from '../../../../shared/ical-subscribe-links/ical-subscribe-links';
import { Card } from '../../../../shared/card/card';

const ROLE_LABELS: Record<CalendarRole, string> = {
  Owner: 'admin.manageCalendars.roles.owner',
  Contributor: 'admin.manageCalendars.roles.contributor',
  Viewer: 'admin.manageCalendars.roles.viewer',
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
  imports: [FormsModule, DatePipe, TranslatePipe, IcalSubscribeLinks, Card],
  templateUrl: './manage-calendars.html',
})
export class ManageCalendars {
  private readonly calendars = inject(CalendarsService);
  private readonly groupsService = inject(GroupsService);

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly timeZoneIds = listTimeZoneIds();

  protected readonly items = resource({
    loader: () => this.calendars.listMyCalendars(),
  });

  // A calendar is always group-owned -- the create form stays hidden behind the needs-group hint
  // until a manageable group has loaded.
  private readonly groups = resource({ loader: () => this.loadManageableGroups() });
  protected readonly manageableGroups = computed<GroupSummary[]>(() => this.groups.value() ?? []);

  protected readonly newCalendarName = signal('');
  protected readonly newCalendarIcon = signal(DEFAULT_ICON);
  protected readonly newCalendarTimeZoneId = signal(resolveDefaultTimeZoneId(this.timeZoneIds));
  // Defaults to the first manageable group once they load.
  protected readonly newCalendarGroupId = linkedSignal(() => this.manageableGroups()[0]?.id ?? '');
  protected readonly creating = createAction();

  protected readonly movingCalendarId = signal<string | null>(null);
  // Stryker disable next-line StringLiteral: startMove() resets it to '' before the move form can render
  protected readonly moveTargetGroupId = signal('');
  protected readonly moving = createAction<string>();

  protected readonly editingIconCalendarId = signal<string | null>(null);
  // Stryker disable next-line StringLiteral: startEditIcon() sets it to the calendar's icon before the edit form can render
  protected readonly editIconValue = signal('');
  protected readonly updatingIcon = createAction<string>();

  protected readonly confirmingDeleteCalendarId = signal<string | null>(null);
  protected readonly deleting = createAction<string>();

  protected readonly icalCalendarId = signal<string | null>(null);
  // Loads the open panel's tokens; idle while no panel is open.
  protected readonly icalTokens = resource({
    params: () => this.icalCalendarId() ?? undefined,
    loader: ({ params }) => this.calendars.listIcalTokens(params),
  });
  protected readonly icalCreating = createAction();
  protected readonly icalRevoking = createAction<string>();
  // The plaintext URL is only ever available right after creation -- once this panel closes or a
  // new token is issued, it's gone from the client just like it's gone from the server.
  protected readonly newIcalUrl = signal<string | null>(null);
  // Stryker disable next-line BooleanLiteral: only read next to newIcalUrl, and createIcalToken() resets it to false before a URL can appear
  protected readonly icalCopied = signal(false);

  protected async createCalendar(): Promise<void> {
    const name = this.newCalendarName().trim();
    // Stryker disable next-line MethodExpression: the value only ever comes from listTimeZoneIds() <option>s, none of which carry whitespace
    const timeZoneId = this.newCalendarTimeZoneId().trim();
    const groupId = this.newCalendarGroupId();
    const icon = this.newCalendarIcon().trim() || null;

    if (!name || !timeZoneId || !groupId) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        await this.calendars.createCalendar({ name, timeZoneId, groupId, icon });
        this.newCalendarName.set('');
        this.newCalendarIcon.set(DEFAULT_ICON);
        this.items.reload();
      },
      'admin.manageCalendars.createError',
    );
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
    this.moving.clearError();
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
    this.updatingIcon.clearError();
  }

  protected async confirmEditIcon(calendarId: string): Promise<void> {
    const icon = this.editIconValue().trim();

    if (!icon) {
      return;
    }

    await this.updatingIcon.run(
      calendarId,
      async () => {
        await this.calendars.updateCalendarIcon(calendarId, icon);
        this.editingIconCalendarId.set(null);
        this.items.reload();
      },
      'admin.manageCalendars.editIcon.error',
    );
  }

  protected async confirmMove(calendarId: string): Promise<void> {
    const groupId = this.moveTargetGroupId();

    if (!groupId) {
      return;
    }

    await this.moving.run(
      calendarId,
      async () => {
        await this.calendars.transferToGroup(calendarId, groupId);
        this.movingCalendarId.set(null);
        this.items.reload();
      },
      'admin.manageCalendars.move.error',
    );
  }

  protected requestDelete(calendarId: string): void {
    this.movingCalendarId.set(null);
    this.icalCalendarId.set(null);
    this.editingIconCalendarId.set(null);
    this.deleting.clearError();
    this.confirmingDeleteCalendarId.set(calendarId);
  }

  protected cancelDelete(): void {
    this.confirmingDeleteCalendarId.set(null);
  }

  protected async confirmDelete(calendarId: string): Promise<void> {
    await this.deleting.run(
      calendarId,
      async () => {
        await this.calendars.deleteCalendar(calendarId);
        this.confirmingDeleteCalendarId.set(null);
        this.items.reload();
      },
      'admin.manageCalendars.delete.error',
    );
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
    this.icalCreating.clearError();
    this.icalRevoking.clearError();
  }

  protected async createIcalToken(calendarId: string): Promise<void> {
    this.newIcalUrl.set(null);
    this.icalCopied.set(false);

    await this.icalCreating.run(
      true,
      async () => {
        const issued = await this.calendars.createIcalToken(calendarId);
        this.newIcalUrl.set(this.calendars.icalFeedUrl(issued.subscriptionPath));
        // The reload replaces whatever the last revoke reported.
        this.icalRevoking.clearError();
        this.icalTokens.reload();
      },
      'admin.manageCalendars.ical.createError',
    );
  }

  protected async revokeIcalToken(calendarId: string, tokenId: string): Promise<void> {
    await this.icalRevoking.run(
      tokenId,
      async () => {
        await this.calendars.revokeIcalToken(calendarId, tokenId);
        this.icalTokens.reload();
      },
      'admin.manageCalendars.ical.revokeError',
    );
  }

  protected async copyIcalUrl(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.icalCopied.set(true);
    } catch {
      this.icalCopied.set(false);
    }
  }

  private async loadManageableGroups(): Promise<GroupSummary[]> {
    try {
      const groups = await this.groupsService.listMyGroups();
      // Group-owned calendar creation is gated on GroupAuthorization.CheckManage server-side,
      // which only Owners and Admins satisfy.
      return groups.filter((group) => group.role === 'Owner' || group.role === 'Admin');
    } catch {
      // No manageable groups, so the create form degrades to the needs-group hint.
      return [];
    }
  }
}
