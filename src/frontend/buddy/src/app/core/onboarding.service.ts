import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { CalendarDetail, CalendarsService } from './calendars.service';
import { addDaysIso, todayIsoDate } from './date-utils';
import { GroupDetail, GroupInvite, GroupMember, GroupsService } from './groups.service';
import { ChildSummary, GuardiansService } from './guardians.service';
import { PER_ITEM_REQUEST_CONCURRENCY, mapWithConcurrency } from './map-with-concurrency';
import { MealplansService } from './mealplans.service';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';
import { TaskLibraryService, TaskTemplate } from './task-library.service';
import { UsersService } from './users.service';

export type OnboardingStatus = Schemas['OnboardingStatus'];

export const ONBOARDING_STATUS = {
  notStarted: 'NotStarted',
  active: 'Active',
  deferred: 'Deferred',
  completed: 'Completed',
} as const satisfies Record<string, OnboardingStatus>;

// The only state the guide stores (GET/PUT /users/me/onboarding). version is the revision read; a
// PUT with an older one answers 409 concurrency_conflict.
export type OnboardingProgress = Schemas['OnboardingProgressResponse'];

export interface SetupTemplate {
  childId: string;
  template: TaskTemplate;
}

// Everything else is derived from current data on each load, starting from the setup group (see
// docs/frontend/analysis/guardian-onboarding.md, "Progress and partial failures").
export interface OnboardingSetup {
  // null before a group is chosen, or when it can no longer be read (groupMissing).
  group: GroupDetail | null;
  groupMissing: boolean;
  // The guardian's children who are members of the setup group, and those not (yet) added to it.
  children: ChildSummary[];
  childrenOutsideGroup: ChildSummary[];
  // Adults in the group other than the signed-in guardian.
  otherAdults: GroupMember[];
  pendingInvites: GroupInvite[];
  calendars: CalendarDetail[];
  templates: SetupTemplate[];
  hasScheduledRoutine: boolean;
  hasMealAssignment: boolean;
}

export const ONBOARDING_STEPS = [
  'group',
  'children',
  'adults',
  'calendar',
  'task',
  'meal',
  'summary',
] as const;

export type OnboardingStep = (typeof ONBOARDING_STEPS)[number];

export const EMPTY_SETUP: OnboardingSetup = {
  group: null,
  groupMissing: false,
  children: [],
  childrenOutsideGroup: [],
  otherAdults: [],
  pendingInvites: [],
  calendars: [],
  templates: [],
  hasScheduledRoutine: false,
  hasMealAssignment: false,
};

// How far back and ahead the guide looks for the task it scheduled (the occurrences API allows a
// year) and the meal it assigned (the meal plan API allows 31 days). The steps only offer dates
// from today up to the look-ahead, so whatever they save is always found again.
const ROUTINE_LOOKBACK_DAYS = 30;
export const ROUTINE_LOOKAHEAD_DAYS = 180;
const MEAL_LOOKBACK_DAYS = 7;
export const MEAL_LOOKAHEAD_DAYS = 23;

// Whether an ISO date is between today and `days` ahead (ISO dates compare as strings).
export function isWithinDaysAhead(isoDate: string, days: number): boolean {
  const today = todayIsoDate();
  return isoDate >= today && isoDate <= addDaysIso(today, days);
}

export function isStepComplete(
  step: OnboardingStep,
  progress: OnboardingProgress,
  setup: OnboardingSetup,
): boolean {
  switch (step) {
    case 'group':
      return setup.group !== null;
    case 'children':
      // Children outside the group (another family's group, or a failed membership) are listed
      // with an "Add to group" action, but don't block: not every child has to be in this group.
      return setup.children.length > 0;
    case 'adults':
      // Invitations count once sent; acceptance isn't required.
      return (
        progress.invitationsSkipped ||
        setup.pendingInvites.length > 0 ||
        setup.otherAdults.length > 0
      );
    case 'calendar':
      return setup.calendars.length > 0;
    case 'task':
      return setup.hasScheduledRoutine;
    case 'meal':
      return setup.hasMealAssignment;
    case 'summary':
      return false;
  }
}

export function firstIncompleteStep(
  progress: OnboardingProgress,
  setup: OnboardingSetup,
): OnboardingStep {
  return ONBOARDING_STEPS.find((step) => !isStepComplete(step, progress, setup)) ?? 'summary';
}

// The guide's own API (progress) plus eligibility and reconciliation over the existing domain
// services. Domain writes stay in those services; this never creates a group, child or calendar.
@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);
  private readonly groups = inject(GroupsService);
  private readonly guardians = inject(GuardiansService);
  private readonly calendars = inject(CalendarsService);
  private readonly taskLibrary = inject(TaskLibraryService);
  private readonly mealplans = inject(MealplansService);
  private readonly users = inject(UsersService);

  private url(): string {
    return `${this.runtimeConfig.apiBaseUrl}/users/me/onboarding`;
  }

  getProgress(): Promise<OnboardingProgress> {
    return firstValueFrom(this.http.get<OnboardingProgress>(this.url()));
  }

  saveProgress(progress: OnboardingProgress): Promise<OnboardingProgress> {
    return firstValueFrom(
      this.http.put<OnboardingProgress>(this.url(), {
        status: progress.status,
        setupGroupId: progress.setupGroupId,
        invitationsSkipped: progress.invitationsSkipped,
        version: progress.version,
      }),
    );
  }

  // Whether a guardian arriving at their home belongs in the guide. An active guide always
  // resumes; a deferred or completed one never auto-starts; a fresh account starts it only with
  // neither groups nor children. Any failed lookup rejects: an error is not an empty result.
  async shouldEnterGuide(): Promise<boolean> {
    const progress = await this.getProgress();

    if (progress.status !== ONBOARDING_STATUS.notStarted) {
      return progress.status === ONBOARDING_STATUS.active;
    }

    const [groups, children] = await Promise.all([
      this.groups.listMyGroups(),
      this.guardians.listMyChildren(),
    ]);

    return groups.length === 0 && children.length === 0;
  }

  async loadSetup(progress: OnboardingProgress): Promise<OnboardingSetup> {
    const groupId = progress.setupGroupId;

    if (groupId === null) {
      return EMPTY_SETUP;
    }

    const group = await this.findGroup(groupId);

    if (group === null) {
      return { ...EMPTY_SETUP, groupMissing: true };
    }

    const today = todayIsoDate();
    const childMemberIds = new Set(
      group.members.filter((member) => member.isChild).map((member) => member.userId),
    );
    const [me, myChildren, pendingInvites, calendars] = await Promise.all([
      this.users.ensureCurrentUser(),
      this.guardians.listMyChildren(),
      this.groups.listInvites(groupId),
      this.listGroupCalendars(groupId),
    ]);
    const children = myChildren.filter((child) => childMemberIds.has(child.id));
    // The family meal plan is addressed through any one of its children.
    const familyChild = children[0];

    const [templates, routineFlags, mealPlan] = await Promise.all([
      mapWithConcurrency(children, PER_ITEM_REQUEST_CONCURRENCY, async (child) =>
        (await this.taskLibrary.listTaskTemplates(child.id))
          .filter((template) => !template.isArchived)
          .map((template) => ({ childId: child.id, template })),
      ),
      mapWithConcurrency(calendars, PER_ITEM_REQUEST_CONCURRENCY, async (calendar) =>
        (
          await this.calendars.listOccurrences(
            calendar.id,
            addDaysIso(today, -ROUTINE_LOOKBACK_DAYS),
            addDaysIso(today, ROUTINE_LOOKAHEAD_DAYS),
          )
        ).some((occurrence) => occurrence.routine !== null),
      ),
      familyChild !== undefined
        ? this.mealplans.listMealPlan(
            { kind: 'family', childId: familyChild.id },
            addDaysIso(today, -MEAL_LOOKBACK_DAYS),
            addDaysIso(today, MEAL_LOOKAHEAD_DAYS),
          )
        : Promise.resolve([]),
    ]);

    return {
      group,
      groupMissing: false,
      children,
      childrenOutsideGroup: myChildren.filter((child) => !childMemberIds.has(child.id)),
      otherAdults: group.members.filter((member) => !member.isChild && member.userId !== me.id),
      pendingInvites,
      calendars,
      templates: templates.flat(),
      hasScheduledRoutine: routineFlags.some(Boolean),
      hasMealAssignment: mealPlan.length > 0,
    };
  }

  // A deleted group, or one the guardian lost access to, is reported as missing so the guide can
  // block the dependent steps and offer a deliberate repair; other failures propagate.
  private async findGroup(groupId: string): Promise<GroupDetail | null> {
    try {
      return await this.groups.getGroup(groupId);
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 403)) {
        return null;
      }
      throw error;
    }
  }

  private async listGroupCalendars(groupId: string): Promise<CalendarDetail[]> {
    const summaries = await this.calendars.listMyCalendars();
    const details = await mapWithConcurrency(summaries, PER_ITEM_REQUEST_CONCURRENCY, (summary) =>
      this.calendars.getCalendar(summary.id),
    );
    return details.filter((calendar) => calendar.groupId === groupId);
  }
}
