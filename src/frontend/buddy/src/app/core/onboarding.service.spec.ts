import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { CalendarDetail, CalendarItemOccurrence, CalendarsService } from './calendars.service';
import { addDaysIso, todayIsoDate } from './date-utils';
import { GroupDetail, GroupsService } from './groups.service';
import { ChildSummary, GuardiansService } from './guardians.service';
import { MealPlanEntry, MealplansService } from './mealplans.service';
import {
  EMPTY_SETUP,
  OnboardingProgress,
  OnboardingService,
  OnboardingSetup,
  firstIncompleteStep,
  isStepComplete,
  isWithinDaysAhead,
} from './onboarding.service';
import { RuntimeConfigService } from './runtime-config.service';
import { TaskLibraryService, TaskTemplate } from './task-library.service';
import { UsersService } from './users.service';

const apiBaseUrl = 'https://api.buddy.test';

const NOT_STARTED: OnboardingProgress = {
  status: 0,
  setupGroupId: null,
  invitationsSkipped: false,
  version: 0,
};

function child(id: string): ChildSummary {
  return {
    id,
    name: { givenName: id, familyName: 'Family' },
    guardianLinkId: `link-${id}`,
    kind: 0,
    language: 'en',
    timeZoneId: 'Europe/Copenhagen',
  };
}

function group(childIds: string[], extra: GroupDetail['members'] = []): GroupDetail {
  return {
    id: 'group-1',
    name: 'Home',
    members: [
      { userId: 'me', givenName: 'Me', familyName: 'Family', role: 0, isChild: false },
      ...childIds.map((id) => ({
        userId: id,
        givenName: id,
        familyName: 'Family',
        role: 2 as const,
        isChild: true,
      })),
      ...extra,
    ],
    calendarPermissionPolicy: { Owner: 0, Admin: 1, Member: 2 },
    mealplanPermissionPolicy: { Owner: 2, Admin: 2, Member: 0 },
  };
}

function template(id: string, isArchived = false): TaskTemplate {
  return {
    id,
    name: id,
    icon: '📋',
    color: '#10b981',
    subtasks: [],
    totalDurationMinutes: 0,
    isArchived,
    createdBy: 'me',
    lastModifiedBy: 'me',
  };
}

interface DomainStubs {
  groups?: Partial<GroupsService>;
  guardians?: Partial<GuardiansService>;
  calendars?: Partial<CalendarsService>;
  taskLibrary?: Partial<TaskLibraryService>;
  mealplans?: Partial<MealplansService>;
}

function setup(stubs: DomainStubs = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: RuntimeConfigService, useValue: { apiBaseUrl } },
      { provide: GroupsService, useValue: stubs.groups ?? {} },
      { provide: GuardiansService, useValue: stubs.guardians ?? {} },
      { provide: CalendarsService, useValue: stubs.calendars ?? {} },
      { provide: TaskLibraryService, useValue: stubs.taskLibrary ?? {} },
      { provide: MealplansService, useValue: stubs.mealplans ?? {} },
      {
        provide: UsersService,
        useValue: { ensureCurrentUser: vi.fn(async () => ({ id: 'me' })) },
      },
    ],
  });

  return {
    service: TestBed.inject(OnboardingService),
    httpMock: TestBed.inject(HttpTestingController),
  };
}

describe('OnboardingService', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  describe('progress API', () => {
    it('GETs the caller’s progress', async () => {
      const { service, httpMock } = setup();

      const promise = service.getProgress();
      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/onboarding`);
      expect(req.request.method).toBe('GET');
      req.flush(NOT_STARTED);

      await expect(promise).resolves.toEqual(NOT_STARTED);
    });

    it('PUTs exactly the stored fields and the version it read', async () => {
      const { service, httpMock } = setup();
      const saved: OnboardingProgress = {
        status: 1,
        setupGroupId: 'group-1',
        invitationsSkipped: true,
        version: 4,
      };

      const promise = service.saveProgress({ ...saved, version: 3 });
      const req = httpMock.expectOne(`${apiBaseUrl}/users/me/onboarding`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        status: 1,
        setupGroupId: 'group-1',
        invitationsSkipped: true,
        version: 3,
      });
      req.flush(saved);

      await expect(promise).resolves.toEqual(saved);
    });
  });

  describe('shouldEnterGuide', () => {
    function eligibility(groupCount: number, childCount: number) {
      return setup({
        groups: {
          listMyGroups: vi.fn(async () =>
            Array.from({ length: groupCount }, (_, i) => ({
              id: `g${i}`,
              name: 'G',
              role: 0 as const,
            })),
          ),
        },
        guardians: {
          listMyChildren: vi.fn(async () =>
            Array.from({ length: childCount }, (_, i) => child(`c${i}`)),
          ),
        },
      });
    }

    async function decide(
      progress: OnboardingProgress,
      groupCount = 0,
      childCount = 0,
    ): Promise<boolean> {
      const { service, httpMock } = eligibility(groupCount, childCount);
      const promise = service.shouldEnterGuide();
      httpMock.expectOne(`${apiBaseUrl}/users/me/onboarding`).flush(progress);
      return promise;
    }

    it.each([
      [0, 0, true],
      [1, 0, false],
      [0, 1, false],
      [2, 3, false],
    ])(
      'with no progress, %i groups and %i children enters the guide: %s',
      async (groups, children, expected) => {
        await expect(decide(NOT_STARTED, groups, children)).resolves.toBe(expected);
      },
    );

    it('resumes an active guide even once the guardian has a group and children', async () => {
      await expect(decide({ ...NOT_STARTED, status: 1, version: 2 }, 1, 2)).resolves.toBe(true);
    });

    it.each([2, 3] as const)(
      'never auto-enters a deferred or completed (%i) guide, even with nothing set up',
      async (status) => {
        await expect(decide({ ...NOT_STARTED, status, version: 1 })).resolves.toBe(false);
      },
    );

    it('rejects rather than treating a failed lookup as an empty account', async () => {
      const { service, httpMock } = setup({
        groups: {
          listMyGroups: vi.fn(async () => {
            throw new HttpErrorResponse({ status: 500 });
          }),
        },
        guardians: { listMyChildren: vi.fn(async () => []) },
      });

      const promise = service.shouldEnterGuide();
      httpMock.expectOne(`${apiBaseUrl}/users/me/onboarding`).flush(NOT_STARTED);

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });

    it('rejects when the progress lookup fails', async () => {
      const { service, httpMock } = setup();

      const promise = service.shouldEnterGuide();
      httpMock
        .expectOne(`${apiBaseUrl}/users/me/onboarding`)
        .flush(null, { status: 403, statusText: 'Forbidden' });

      await expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);
    });
  });

  describe('loadSetup', () => {
    const progress: OnboardingProgress = {
      ...NOT_STARTED,
      status: 1,
      setupGroupId: 'group-1',
      version: 2,
    };

    function calendar(id: string, groupId: string): CalendarDetail {
      return { id, name: id, icon: '📅', timeZoneId: 'UTC', groupId, members: [] };
    }

    function domain(overrides: DomainStubs = {}): DomainStubs {
      return {
        groups: {
          getGroup: vi.fn(async () => group(['c1'])),
          listInvites: vi.fn(async () => []),
          ...overrides.groups,
        },
        guardians: { listMyChildren: vi.fn(async () => [child('c1')]), ...overrides.guardians },
        calendars: {
          listMyCalendars: vi.fn(async () => [
            { id: 'cal-1', name: 'Family', icon: '📅', role: 0 as const },
            { id: 'cal-2', name: 'Work', icon: '📅', role: 0 as const },
          ]),
          getCalendar: vi.fn(async (id: string) =>
            calendar(id, id === 'cal-1' ? 'group-1' : 'other-group'),
          ),
          listOccurrences: vi.fn(async () => [] as CalendarItemOccurrence[]),
          ...overrides.calendars,
        },
        taskLibrary: { listTaskTemplates: vi.fn(async () => []), ...overrides.taskLibrary },
        mealplans: { listMealPlan: vi.fn(async () => []), ...overrides.mealplans },
      };
    }

    it('is empty before a setup group is chosen, without any request', async () => {
      const stubs = domain();
      const { service } = setup(stubs);

      await expect(service.loadSetup(NOT_STARTED)).resolves.toEqual(EMPTY_SETUP);
      expect(stubs.groups!.getGroup).not.toHaveBeenCalled();
    });

    it.each([404, 403])('reports a group answering %i as missing', async (status) => {
      const { service } = setup(
        domain({
          groups: {
            getGroup: vi.fn(async () => {
              throw new HttpErrorResponse({ status });
            }),
          },
        }),
      );

      await expect(service.loadSetup(progress)).resolves.toEqual({
        ...EMPTY_SETUP,
        groupMissing: true,
      });
    });

    it('propagates any other failure to read the group', async () => {
      const { service } = setup(
        domain({
          groups: {
            getGroup: vi.fn(async () => {
              throw new HttpErrorResponse({ status: 500 });
            }),
          },
        }),
      );

      await expect(service.loadSetup(progress)).rejects.toBeInstanceOf(HttpErrorResponse);
    });

    it('splits the guardian’s children into group members and those still outside it', async () => {
      const { service } = setup(
        domain({ guardians: { listMyChildren: vi.fn(async () => [child('c1'), child('c2')]) } }),
      );

      const result = await service.loadSetup(progress);

      expect(result.children.map((c) => c.id)).toEqual(['c1']);
      expect(result.childrenOutsideGroup.map((c) => c.id)).toEqual(['c2']);
    });

    it('counts other adults in the group, never the guardian themselves', async () => {
      const { service } = setup(
        domain({
          groups: {
            getGroup: vi.fn(async () =>
              group(
                ['c1'],
                [{ userId: 'aunt', givenName: 'A', familyName: 'F', role: 2, isChild: false }],
              ),
            ),
          },
        }),
      );

      const result = await service.loadSetup(progress);

      expect(result.otherAdults.map((member) => member.userId)).toEqual(['aunt']);
    });

    it('keeps only the setup group’s calendars', async () => {
      const { service } = setup(domain());

      const result = await service.loadSetup(progress);

      expect(result.calendars.map((c) => c.id)).toEqual(['cal-1']);
    });

    it('collects the setup children’s live templates', async () => {
      const listTaskTemplates = vi.fn(async () => [template('t1'), template('t2', true)]);
      const { service } = setup(domain({ taskLibrary: { listTaskTemplates } }));

      const result = await service.loadSetup(progress);

      expect(listTaskTemplates).toHaveBeenCalledWith('c1');
      expect(result.templates).toEqual([{ childId: 'c1', template: template('t1') }]);
    });

    it('finds a routine scheduled on the setup calendar within the look-around window', async () => {
      const today = todayIsoDate();
      const listOccurrences = vi.fn(async () => [
        { routine: null } as CalendarItemOccurrence,
        {
          routine: { subtaskId: 's1', parentTitle: 'Morning', parentIcon: '🌅' },
        } as CalendarItemOccurrence,
      ]);
      const { service } = setup(domain({ calendars: { listOccurrences } }));

      const result = await service.loadSetup(progress);

      expect(listOccurrences).toHaveBeenCalledWith(
        'cal-1',
        addDaysIso(today, -30),
        addDaysIso(today, 180),
      );
      expect(result.hasScheduledRoutine).toBe(true);
    });

    it('does not count plain tasks or events as the routine', async () => {
      const { service } = setup(
        domain({
          calendars: {
            listOccurrences: vi.fn(async () => [{ routine: null } as CalendarItemOccurrence]),
          },
        }),
      );

      expect((await service.loadSetup(progress)).hasScheduledRoutine).toBe(false);
    });

    it('reads the family meal plan through the first setup child', async () => {
      const today = todayIsoDate();
      const listMealPlan = vi.fn(async () => [{ mealId: 'm1' } as MealPlanEntry]);
      const { service } = setup(domain({ mealplans: { listMealPlan } }));

      const result = await service.loadSetup(progress);

      expect(listMealPlan).toHaveBeenCalledWith(
        { kind: 'family', childId: 'c1' },
        addDaysIso(today, -7),
        addDaysIso(today, 23),
      );
      expect(result.hasMealAssignment).toBe(true);
    });

    it('does not read a meal plan while the group has no children', async () => {
      const listMealPlan = vi.fn(async () => []);
      const { service } = setup(
        domain({
          groups: { getGroup: vi.fn(async () => group([])) },
          guardians: { listMyChildren: vi.fn(async () => []) },
          mealplans: { listMealPlan },
        }),
      );

      expect((await service.loadSetup(progress)).hasMealAssignment).toBe(false);
      expect(listMealPlan).not.toHaveBeenCalled();
    });
  });
});

describe('onboarding step completion', () => {
  const active: OnboardingProgress = {
    ...NOT_STARTED,
    status: 1,
    setupGroupId: 'group-1',
    version: 1,
  };
  const complete: OnboardingSetup = {
    group: group(['c1']),
    groupMissing: false,
    children: [child('c1')],
    childrenOutsideGroup: [],
    otherAdults: [],
    pendingInvites: [{ id: 'i1', email: 'a@b.test', role: 1, invitedAt: '', expiresAt: '' }],
    calendars: [
      { id: 'cal-1', name: 'F', icon: '📅', timeZoneId: 'UTC', groupId: 'group-1', members: [] },
    ],
    templates: [],
    hasScheduledRoutine: true,
    hasMealAssignment: true,
  };

  it('starts at the group step', () => {
    expect(firstIncompleteStep(NOT_STARTED, EMPTY_SETUP)).toBe('group');
  });

  it('lands on the summary once every step is done', () => {
    expect(firstIncompleteStep(active, complete)).toBe('summary');
  });

  it('needs a child in the group, but not every child the guardian has', () => {
    expect(isStepComplete('children', active, { ...complete, children: [] })).toBe(false);
    expect(
      isStepComplete('children', active, { ...complete, childrenOutsideGroup: [child('c2')] }),
    ).toBe(true);
  });

  it('counts the adults step done when skipped, invited, or another adult already joined', () => {
    const none = { ...complete, pendingInvites: [] };
    expect(isStepComplete('adults', active, none)).toBe(false);
    expect(isStepComplete('adults', { ...active, invitationsSkipped: true }, none)).toBe(true);
    expect(isStepComplete('adults', active, complete)).toBe(true);
    expect(
      isStepComplete('adults', active, {
        ...none,
        otherAdults: [{ userId: 'aunt', givenName: 'A', familyName: 'F', role: 2, isChild: false }],
      }),
    ).toBe(true);
  });

  it('resumes at the first missing step', () => {
    expect(firstIncompleteStep(active, { ...complete, calendars: [] })).toBe('calendar');
    expect(firstIncompleteStep(active, { ...complete, hasScheduledRoutine: false })).toBe('task');
    expect(firstIncompleteStep(active, { ...complete, hasMealAssignment: false })).toBe('meal');
  });

  it('never treats the summary itself as done', () => {
    expect(isStepComplete('summary', active, complete)).toBe(false);
  });
});

describe('isWithinDaysAhead', () => {
  it('accepts today through the look-ahead, and nothing before or after', () => {
    const today = todayIsoDate();
    expect(isWithinDaysAhead(today, 23)).toBe(true);
    expect(isWithinDaysAhead(addDaysIso(today, 23), 23)).toBe(true);
    expect(isWithinDaysAhead(addDaysIso(today, 24), 23)).toBe(false);
    expect(isWithinDaysAhead(addDaysIso(today, -1), 23)).toBe(false);
  });
});
