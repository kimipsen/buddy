import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarRole, CalendarsService } from '../../../../core/calendars.service';
import { browserTimeZoneId, listTimeZoneIds } from '../../../../core/date-utils';
import { GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import { createAction } from '../../../../shared/action-state/action-state';

const ROLE_LABELS: Record<CalendarRole, string> = {
  0: 'onboarding.calendar.roles.owner',
  1: 'onboarding.calendar.roles.contributor',
  2: 'onboarding.calendar.roles.viewer',
};

// Matches the backend's Calendar.DefaultIcon, like the admin page's calendar form.
const DEFAULT_ICON = '📅';
const VIEWER: CalendarRole = 2;

// Same fallback as the admin calendar form: the browser may report an alias ("UTC") the list
// doesn't contain, and the select must only offer values it shows.
function defaultTimeZoneId(candidates: readonly string[]): string {
  const browserZone = browserTimeZoneId();
  return candidates.includes(browserZone) ? browserZone : (candidates[0] ?? browserZone);
}

// Step 4: a calendar owned by the setup group. Sharing means the group's own permission policy,
// read as it is rather than assumed; making children view-only is an explicit choice.
@Component({
  selector: 'app-onboarding-calendar-step',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './calendar-step.html',
})
export class CalendarStep {
  private readonly calendars = inject(CalendarsService);
  private readonly groups = inject(GroupsService);

  readonly setup = input.required<OnboardingSetup>();
  readonly changed = output<void>();

  protected readonly roleLabels = ROLE_LABELS;
  protected readonly timeZoneIds = listTimeZoneIds();

  protected readonly name = signal('');
  protected readonly icon = signal(DEFAULT_ICON);
  protected readonly timeZoneId = signal(defaultTimeZoneId(this.timeZoneIds));
  protected readonly creating = createAction();
  protected readonly updatingPolicy = createAction();

  protected readonly policy = computed(() => this.setup().group?.calendarPermissionPolicy ?? null);
  protected readonly membersCanEdit = computed(() => {
    const policy = this.policy();
    return policy !== null && policy.Member !== VIEWER;
  });

  protected async create(): Promise<void> {
    const name = this.name().trim();
    const groupId = this.setup().group?.id;

    if (!name || groupId === undefined) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        await this.calendars.createCalendar({
          name,
          timeZoneId: this.timeZoneId(),
          groupId,
          icon: this.icon().trim() || null,
        });
        this.name.set('');
        this.changed.emit();
      },
      'onboarding.calendar.createError',
    );
  }

  protected async makeChildrenViewOnly(): Promise<void> {
    const group = this.setup().group;

    if (group === null) {
      return;
    }

    await this.updatingPolicy.run(
      true,
      async () => {
        await this.groups.updateCalendarPermissionPolicy(group.id, {
          ...group.calendarPermissionPolicy,
          Member: VIEWER,
        });
        this.changed.emit();
      },
      'onboarding.calendar.policyError',
    );
  }
}
