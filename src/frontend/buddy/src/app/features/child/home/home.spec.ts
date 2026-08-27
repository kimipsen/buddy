import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth.service';
import { CalendarOccurrence, CalendarsService, TaskCompletion } from '../../../core/calendars.service';
import { todayIsoDate } from '../../../core/date-utils';
import { GuardianSummary, GuardiansService, SiblingSummary } from '../../../core/guardians.service';
import { MealPlanEntry, MealplansService } from '../../../core/mealplans.service';
import { MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { ProgressService } from '../../../core/progress.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildHome } from './home';

describe('ChildHome', () => {
  const currentUser: CurrentUser = {
    id: 'child-1',
    email: { value: 'kid@buddy.test', isVerified: true },
    userName: 'kid',
    name: { givenName: 'Kim', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en'
  };

  const today = todayIsoDate();

  function mealEntry(overrides: Partial<MealPlanEntry> = {}): MealPlanEntry {
    return {
      date: today,
      slot: 0,
      mealId: 'meal-1',
      mealName: 'Pancakes',
      icon: '🥞',
      color: '#f00',
      rating: null,
      notes: null,
      assignedBy: 'guardian-1',
      allRatings: [],
      ...overrides
    };
  }

  interface Stubs {
    auth?: Partial<AuthService>;
    guardians?: Partial<GuardiansService>;
    pickups?: Partial<PickupsService>;
    users?: Partial<UsersService>;
    mealplans?: Partial<MealplansService>;
    medicines?: Partial<MedicinesService>;
    calendars?: Partial<CalendarsService>;
    progress?: Partial<ProgressService>;
  }

  async function setup(stubs: Stubs = {}) {
    const authStub: Partial<AuthService> = { logout: vi.fn(), ...stubs.auth };
    const guardiansStub: Partial<GuardiansService> = {
      listMyGuardians: vi.fn(async () => []),
      listMySiblings: vi.fn(async () => []),
      ...stubs.guardians
    };
    const pickupsStub: Partial<PickupsService> = { listSchedule: vi.fn(async () => []), ...stubs.pickups };
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => currentUser),
      timeZoneId: signal('UTC').asReadonly(),
      ...stubs.users
    };
    const mealplansStub: Partial<MealplansService> = {
      listMealPlan: vi.fn(async () => []),
      rateMeal: vi.fn(),
      ...stubs.mealplans
    };
    const medicinesStub: Partial<MedicinesService> = {
      listDoses: vi.fn(async () => []),
      setDoseStatus: vi.fn(),
      ...stubs.medicines
    };
    const calendarsStub: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(async () => []),
      setTaskCompletion: vi.fn(),
      ...stubs.calendars
    };
    const progressStub: Partial<ProgressService> = {
      getMyProgress: vi.fn(async () => ({ totalStars: 0, unlockedMilestones: [] })),
      ...stubs.progress
    };

    await TestBed.configureTestingModule({
      imports: [ChildHome],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub },
        { provide: UsersService, useValue: usersStub },
        { provide: MealplansService, useValue: mealplansStub },
        { provide: MedicinesService, useValue: medicinesStub },
        { provide: CalendarsService, useValue: calendarsStub },
        { provide: ProgressService, useValue: progressStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildHome);

    return {
      fixture,
      auth: authStub,
      guardians: guardiansStub,
      pickups: pickupsStub,
      users: usersStub,
      mealplans: mealplansStub,
      medicines: medicinesStub,
      calendars: calendarsStub,
      progress: progressStub
    };
  }

  // The app runs zoneless, and none of these stubbed services register a PendingTasks entry, so
  // fixture.whenStable() resolves immediately without actually waiting for them. A macrotask
  // flush lets every already-scheduled microtask in the mocked promise chains drain first.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function findButtonByAriaLabel(compiled: HTMLElement, label: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.getAttribute('aria-label') === label);
  }

  it('shows the loading spinner while the dashboard is still loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with nothing to show', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing to show yet');
  });

  it('shows the translated error message when loading the dashboard fails', async () => {
    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong. Try again in a bit.');
  });

  it('signs the child out when the sign out button is clicked', async () => {
    const { fixture, auth } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Sign out')?.click();

    expect(auth.logout).toHaveBeenCalled();
  });

  it('renders today\'s guardians once loaded', async () => {
    const guardianList: GuardianSummary[] = [{ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' }, guardianLinkId: 'link-1', kind: 0 }];
    const { fixture } = await setup({ guardians: { listMyGuardians: vi.fn(async () => guardianList) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Gina');
  });

  it('rates a meal and reflects the rating on every slot sharing that meal', async () => {
    const entries = [mealEntry({ slot: 0 }), mealEntry({ slot: 1 })];
    const rateMeal = vi.fn(async () => ({
      id: 'meal-1',
      name: 'Pancakes',
      description: null,
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 4, comment: null, ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1'
    }));

    const { fixture, mealplans } = await setup({ mealplans: { listMealPlan: vi.fn(async () => entries), rateMeal } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const starButtons = Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[aria-label^="Rate"]'));
    expect(starButtons).toHaveLength(10);

    starButtons[3].click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 4, null);
    expect(starButtons[3].classList.contains('text-amber-400')).toBe(true);
    expect(starButtons[8].classList.contains('text-amber-400')).toBe(true);
  });

  it('adds a note to a meal', async () => {
    const rateMeal = vi.fn(async () => ({
      id: 'meal-1',
      name: 'Pancakes',
      description: null,
      icon: '🥞',
      color: '#f00',
      isArchived: false,
      ratings: [{ childId: 'child-1', stars: 5, comment: 'Yummy!', ratedAt: '2026-01-01T00:00:00Z' }],
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1'
    }));

    const { fixture } = await setup({ mealplans: { listMealPlan: vi.fn(async () => [mealEntry()]), rateMeal } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Add a note')?.click();
    fixture.detectChanges();

    const textarea = compiled.querySelector<HTMLTextAreaElement>('textarea')!;
    textarea.value = 'Yummy!';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    findButtonByText(compiled, 'Save')?.click();
    await settle(fixture);

    expect(rateMeal).toHaveBeenCalledWith('child-1', 'meal-1', 5, 'Yummy!');
    expect(compiled.querySelector('textarea')).toBeFalsy();
    expect(compiled.textContent).toContain('Yummy!');
  });

  it('marks a medicine dose as taken', async () => {
    const dose: MedicineDoseOccurrence = { medicineId: 'med-1', name: 'Vitamin', dosage: '1 tablet', icon: '💊', color: '#0f0', date: today, time: '09:00:00', status: 0 };
    const setDoseStatus = vi.fn(async () => ({ ...dose, status: 1 as const }));

    const { fixture, medicines } = await setup({ medicines: { listDoses: vi.fn(async () => [dose]), setDoseStatus } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Taken')?.click();
    await settle(fixture);

    expect(medicines.setDoseStatus).toHaveBeenCalledWith('child-1', 'med-1', today, '09:00:00', 1);
    expect(compiled.textContent).toContain('Taken ✓');
  });

  it('toggles a task\'s completion', async () => {
    const task: CalendarOccurrence = {
      itemId: 'task-1',
      kind: 1,
      title: 'Clean room',
      icon: '🧹',
      iconOverride: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: 'child-1',
      calendarId: 'cal-1',
      calendarName: 'Home'
    };
    const completion: TaskCompletion = { itemId: 'task-1', occurrenceDate: today, isCompleted: true };
    const setTaskCompletion = vi.fn(async () => completion);

    const { fixture, calendars } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [task]), setTaskCompletion } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
    expect(findButtonByAriaLabel(compiled, 'Mark not done')).toBeTruthy();
  });

  it('completing one subtask of a template-scheduled run does not flip its sibling subtasks (the compound-key fix)', async () => {
    function subtask(subtaskId: string, title: string): CalendarOccurrence {
      return {
        itemId: 'run-1',
        kind: 1,
        title,
        icon: '🧹',
        iconOverride: null,
        color: '#000',
        startsAt: null,
        endsAt: null,
        dueAt: null,
        isAllDay: false,
        isCompleted: false,
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1',
        calendarId: 'cal-1',
        calendarName: 'Home',
        parentTitle: 'Morning routine',
        subtaskId
      };
    }

    const subtasks = [subtask('sub-1', 'Brush teeth'), subtask('sub-2', 'Get dressed')];
    const setTaskCompletion = vi.fn(async () => ({ itemId: 'run-1', occurrenceDate: today, isCompleted: true }) as TaskCompletion);

    const { fixture, calendars } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => subtasks), setTaskCompletion }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggles = compiled.querySelectorAll('button[aria-label="Mark done"]');
    expect(toggles).toHaveLength(2);

    toggles[0].dispatchEvent(new Event('click'));
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'run-1', today, true, 'sub-1');

    const afterToggle = (fixture.nativeElement as HTMLElement).querySelectorAll('button[aria-label]');
    const doneCount = Array.from(afterToggle).filter((button) => button.getAttribute('aria-label') === 'Mark not done').length;
    // Only the toggled subtask flips to "Mark not done" -- the sibling stays "Mark done".
    expect(doneCount).toBe(1);
  });

  it('groups a template-scheduled run\'s subtasks under their parent task\'s title', async () => {
    function subtask(subtaskId: string, title: string): CalendarOccurrence {
      return {
        itemId: 'run-1',
        kind: 1,
        title,
        icon: '🧹',
        iconOverride: null,
        color: '#000',
        startsAt: null,
        endsAt: null,
        dueAt: null,
        isAllDay: false,
        isCompleted: false,
        createdBy: 'guardian-1',
        lastModifiedBy: 'guardian-1',
        assignedTo: 'child-1',
        calendarId: 'cal-1',
        calendarName: 'Home',
        parentTitle: 'Go to bed',
        subtaskId
      };
    }

    const subtasks = [subtask('sub-1', 'Brush teeth'), subtask('sub-2', 'Put on pajamas')];

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => subtasks) } });
    await settle(fixture);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Go to bed');
    expect(text).toContain('Brush teeth');
    expect(text).toContain('Put on pajamas');
  });

  it('shows today\'s events including their time', async () => {
    const event: CalendarOccurrence = {
      itemId: 'event-1',
      kind: 0,
      title: 'Soccer practice',
      icon: '⚽',
      iconOverride: null,
      color: '#00f',
      startsAt: `${today}T16:00:00Z`,
      endsAt: `${today}T17:00:00Z`,
      dueAt: null,
      isAllDay: false,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home'
    };

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [event]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Events today');
    expect(compiled.textContent).toContain('Soccer practice');
    expect(compiled.textContent).toContain('4:00');
  });

  it('links to the full child calendar', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const link = Array.from(compiled.querySelectorAll('a')).find((anchor) => anchor.getAttribute('href') === '/child/calendar');
    expect(link).toBeTruthy();
  });

  it('resolves the assignee name for a guardian pickup occurrence', async () => {
    const guardianList: GuardianSummary[] = [{ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' }, guardianLinkId: 'link-1', kind: 0 }];
    const occurrence: PickupOccurrence = {
      date: today,
      slot: 0,
      kind: 0,
      guardianId: 'guardian-1',
      siblingChildId: null,
      playdateHostName: null,
      playdateLocation: null,
      playdateContactInfo: null,
      time: '08:00:00',
      notes: null,
      assignedBy: 'guardian-1'
    };

    const { fixture } = await setup({
      guardians: { listMyGuardians: vi.fn(async () => guardianList) },
      pickups: { listSchedule: vi.fn(async () => [occurrence]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Drop-off');
    expect(compiled.textContent).toContain('Gina');
  });

  it('resolves the assignee name for a sibling pickup occurrence', async () => {
    const siblingList: SiblingSummary[] = [{ id: 'sib-1', name: { givenName: 'Sam', familyName: 'S' } }];
    const occurrence: PickupOccurrence = {
      date: today,
      slot: 1,
      kind: 2,
      guardianId: null,
      siblingChildId: 'sib-1',
      playdateHostName: null,
      playdateLocation: null,
      playdateContactInfo: null,
      time: null,
      notes: null,
      assignedBy: 'guardian-1'
    };

    const { fixture } = await setup({
      guardians: { listMySiblings: vi.fn(async () => siblingList) },
      pickups: { listSchedule: vi.fn(async () => [occurrence]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Sam');
  });
});
