import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AssignableMember, CalendarOccurrence, CalendarsService, TaskCompletion } from '../../../core/calendars.service';
import { toIsoDateInTimeZone } from '../../../core/date-utils';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { TasksToday } from './tasks-today';

describe('TasksToday', () => {
  const currentUser: CurrentUser = {
    id: 'guardian-1',
    email: { value: 'guardian@buddy.test', isVerified: true },
    userName: 'guardian',
    name: { givenName: 'Gina', familyName: 'G' },
    timeZoneId: 'UTC',
    language: 'en'
  };

  const today = toIsoDateInTimeZone(new Date(), currentUser.timeZoneId);

  function task(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'task-1',
      kind: 1,
      title: 'Clean room',
      icon: '🧹',
      iconOverride: null,
      color: '#000',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T00:00:00Z`,
      isAllDay: true,
      isCompleted: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo: null,
      calendarId: 'cal-1',
      calendarName: 'Home',
      ...overrides
    };
  }

  interface Stubs {
    users?: Partial<UsersService>;
    calendars?: Partial<CalendarsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const usersStub: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(async () => currentUser),
      timeZoneId: signal(currentUser.timeZoneId).asReadonly(),
      ...stubs.users
    };
    const calendarsStub: Partial<CalendarsService> = {
      listTodayOccurrences: vi.fn(async () => []),
      listAssignableMembers: vi.fn(async () => []),
      setTaskCompletion: vi.fn(),
      ...stubs.calendars
    };

    await TestBed.configureTestingModule({
      imports: [TasksToday],
      providers: [provideRouter([]), { provide: UsersService, useValue: usersStub }, { provide: CalendarsService, useValue: calendarsStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(TasksToday);

    return { fixture, users: usersStub, calendars: calendarsStub };
  }

  // loadTasks and toggleTask each chain more than one await (Promise.all, a rejection sometimes
  // taking an extra microtask turn to propagate, a follow-up fetch for assignee names) before the
  // signals driving the template settle -- a single whenStable() flush isn't always enough, so
  // flush a generous fixed number of times rather than guessing when it's "probably" done.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findCheckbox(compiled: HTMLElement, title: string): HTMLInputElement | undefined {
    return compiled.querySelector<HTMLInputElement>(`input[type="checkbox"][aria-label="${title}"]`) ?? undefined;
  }

  it('shows the loading spinner while tasks are loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with no tasks', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No tasks due today.');
  });

  it('shows the translated error message when loading tasks fails', async () => {
    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s tasks.');
  });

  it('does not treat an all-day task due today as overdue', async () => {
    const allDayTask = task({ itemId: 'all-day', title: 'All day chore', isAllDay: true, dueAt: `${today}T00:00:00Z` });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [allDayTask]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Overdue');
    expect(compiled.textContent).toContain('Due today');
    expect(compiled.textContent).toContain('All day chore');
  });

  it('treats a timed task past its due time today as overdue, and one not yet due as due today', async () => {
    const overdueTask = task({
      itemId: 'overdue-task',
      title: 'Past due chore',
      isAllDay: false,
      dueAt: new Date(Date.now() - 60 * 60 * 1000).toISOString()
    });
    const upcomingTask = task({
      itemId: 'upcoming-task',
      title: 'Later chore',
      isAllDay: false,
      dueAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
    });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [overdueTask, upcomingTask]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Overdue');
    expect(compiled.textContent).toContain('Past due chore');
    expect(compiled.textContent).toContain('Due today');
    expect(compiled.textContent).toContain('Later chore');
  });

  it('shows a completed task with a checked, strikethrough checkbox', async () => {
    const completedTask = task({ title: 'Done chore', isCompleted: true });

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [completedTask]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const checkbox = findCheckbox(compiled, 'Done chore');
    expect(checkbox?.checked).toBe(true);

    const title = Array.from(compiled.querySelectorAll('span')).find((span) => span.textContent?.trim() === 'Done chore');
    expect(title?.classList.contains('line-through')).toBe(true);
  });

  it('shows the assignee name for an assigned task when it can be resolved', async () => {
    const assignedTask = task({ title: 'Take out trash', assignedTo: 'child-1' });
    const members: AssignableMember[] = [{ userId: 'child-1', givenName: 'Sam', familyName: 'Kid' }];

    const { fixture } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => [assignedTask]), listAssignableMembers: vi.fn(async () => members) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Sam Kid');
  });

  it('allows toggling an unassigned task and marks it done', async () => {
    const unassignedTask = task({ title: 'Water plants', assignedTo: null });
    const completion: TaskCompletion = { itemId: 'task-1', occurrenceDate: today, isCompleted: true };
    const setTaskCompletion = vi.fn(async () => completion);

    const { fixture, calendars } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => [unassignedTask]), setTaskCompletion }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const checkbox = findCheckbox(compiled, 'Water plants')!;
    expect(checkbox.disabled).toBe(false);

    checkbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
    expect(findCheckbox(compiled, 'Water plants')?.checked).toBe(true);
  });

  it('allows toggling a task assigned to the signed-in guardian', async () => {
    const ownTask = task({ title: 'Pack lunch', assignedTo: 'guardian-1' });
    const setTaskCompletion = vi.fn(async () => ({ itemId: 'task-1', occurrenceDate: today, isCompleted: true }) as TaskCompletion);

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [ownTask]), setTaskCompletion } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const checkbox = findCheckbox(compiled, 'Pack lunch')!;
    expect(checkbox.disabled).toBe(false);

    checkbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
  });

  it('disables the checkbox for a task assigned to someone else and refuses to toggle it', async () => {
    const othersTask = task({ title: 'Feed the dog', assignedTo: 'other-guardian' });
    const setTaskCompletion = vi.fn();

    const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => [othersTask]), setTaskCompletion } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const checkbox = findCheckbox(compiled, 'Feed the dog')!;
    expect(checkbox.disabled).toBe(true);

    checkbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(setTaskCompletion).not.toHaveBeenCalled();
  });

  // ----- Multi-subtask rollup -----

  describe('rolling up a template-scheduled run', () => {
    function subtaskOf(run: string, subtaskId: string, overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
      return task({ itemId: run, subtaskId, parentTitle: 'Morning routine', ...overrides });
    }

    it('shows a single row with a fraction-complete badge instead of one row per subtask', async () => {
      const subtasks = [
        subtaskOf('run-1', 'sub-1', { title: 'Brush teeth', isCompleted: true, dueAt: `${today}T08:00:00Z`, isAllDay: false }),
        subtaskOf('run-1', 'sub-2', { title: 'Get dressed', isCompleted: false, dueAt: `${today}T08:10:00Z`, isAllDay: false }),
        subtaskOf('run-1', 'sub-3', { title: 'Eat breakfast', isCompleted: false, dueAt: `${today}T08:20:00Z`, isAllDay: false })
      ];

      const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => subtasks) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent?.match(/Morning routine/g)?.length).toBe(1);
      expect(compiled.textContent).not.toContain('Brush teeth');
      expect(compiled.textContent).toContain('1 of 3 done');
    });

    it('does not render a checkbox for a rolled-up multi-subtask row', async () => {
      const subtasks = [
        subtaskOf('run-1', 'sub-1', { title: 'Brush teeth', dueAt: `${today}T08:00:00Z`, isAllDay: false }),
        subtaskOf('run-1', 'sub-2', { title: 'Get dressed', dueAt: `${today}T08:10:00Z`, isAllDay: false })
      ];

      const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => subtasks) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('input[type="checkbox"]')).toBeNull();
    });

    it('is not yet overdue while its LAST subtask is still in the future, even if an earlier one is already past its own time', async () => {
      const notYetOverdue = [
        subtaskOf('run-1', 'sub-1', {
          title: 'Brush teeth',
          dueAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
          isAllDay: false
        }),
        subtaskOf('run-1', 'sub-2', {
          title: 'Eat breakfast',
          dueAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          isAllDay: false
        })
      ];

      const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => notYetOverdue) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).not.toContain('Overdue');
      expect(compiled.textContent).toContain('Due today');
    });

    it('becomes overdue once its LAST subtask\'s due time has passed', async () => {
      const bothOverdue = [
        subtaskOf('run-2', 'sub-1', {
          title: 'Brush teeth',
          dueAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
          isAllDay: false
        }),
        subtaskOf('run-2', 'sub-2', {
          title: 'Eat breakfast',
          dueAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
          isAllDay: false
        })
      ];

      const { fixture } = await setup({ calendars: { listTodayOccurrences: vi.fn(async () => bothOverdue) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Overdue');
    });
  });

  it('shows an error and keeps the list visible when toggling a task fails', async () => {
    const unassignedTask = task({ title: 'Water plants', assignedTo: null });
    const setTaskCompletion = vi.fn(async () => Promise.reject(new Error('boom')));

    const { fixture } = await setup({
      calendars: { listTodayOccurrences: vi.fn(async () => [unassignedTask]), setTaskCompletion }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findCheckbox(compiled, 'Water plants')!.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to update this task.');
    expect(compiled.textContent).toContain('Water plants');
  });
});
