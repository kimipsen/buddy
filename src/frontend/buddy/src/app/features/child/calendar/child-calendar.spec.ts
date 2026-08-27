import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { CalendarOccurrence, CalendarSummary, CalendarsService } from '../../../core/calendars.service';
import { toIsoDate, todayIsoDate } from '../../../core/date-utils';
import { UsersService } from '../../../core/users.service';
import { ChildCalendar } from './child-calendar';

describe('ChildCalendar', () => {
  const today = todayIsoDate();

  // Mirrors the component's own local-date arithmetic (parseIsoDate + toIsoDate) so expectations
  // don't depend on the host machine's time zone.
  function addDays(isoDate: string, days: number): string {
    const [year, month, day] = isoDate.split('-').map(Number);
    return toIsoDate(new Date(year, month - 1, day + days));
  }

  function calendarSummary(overrides: Partial<CalendarSummary> = {}): CalendarSummary {
    return { id: 'cal-1', name: 'Home', icon: '🏠', role: 2, ...overrides };
  }

  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'item-1',
      kind: 0,
      title: 'Dentist',
      icon: '🦷',
      iconOverride: null,
      color: '#112233',
      startsAt: `${today}T09:00:00Z`,
      endsAt: `${today}T10:00:00Z`,
      dueAt: null,
      isAllDay: false,
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
      timeZoneId: signal('UTC').asReadonly(),
      ...stubs.users
    };
    const calendarsStub: Partial<CalendarsService> = {
      listMyCalendars: vi.fn(async () => [calendarSummary()]),
      listOccurrencesInRange: vi.fn(async () => []),
      setTaskCompletion: vi.fn(async () => ({ itemId: 'item-1', occurrenceDate: today, isCompleted: true })),
      ...stubs.calendars
    };

    await TestBed.configureTestingModule({
      imports: [ChildCalendar],
      providers: [provideRouter([]), { provide: UsersService, useValue: usersStub }, { provide: CalendarsService, useValue: calendarsStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildCalendar);

    return { fixture, calendars: calendarsStub };
  }

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByAriaLabel(compiled: HTMLElement, label: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.getAttribute('aria-label') === label);
  }

  it('shows the loading message before the week resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading your calendar…');
  });

  it('shows the empty state once loading finishes with no occurrences', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing planned this week');
  });

  it('shows the translated error message when listOccurrencesInRange rejects', async () => {
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Something went wrong loading your calendar');
  });

  it('requests the current week (today through six days ahead) on first load', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith(today, addDays(today, 6));
  });

  it('groups occurrences under the correct day and orders same-day items by time', async () => {
    const tomorrow = addDays(today, 1);
    const early = occurrence({ itemId: 'early', title: 'Early meeting', startsAt: `${today}T08:00:00Z`, endsAt: `${today}T08:30:00Z` });
    const late = occurrence({ itemId: 'late', title: 'Late meeting', startsAt: `${today}T18:00:00Z`, endsAt: `${today}T18:30:00Z` });
    const nextDay = occurrence({ itemId: 'tomorrow', title: 'Tomorrow item', startsAt: `${tomorrow}T09:00:00Z`, endsAt: `${tomorrow}T09:30:00Z` });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [late, nextDay, early]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const text = compiled.textContent ?? '';
    expect(text.indexOf('Early meeting')).toBeLessThan(text.indexOf('Late meeting'));
    expect(text.indexOf('Late meeting')).toBeLessThan(text.indexOf('Tomorrow item'));
  });

  it('groups a task by its due date and shows a completion toggle', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
      assignedTo: 'child-1'
    });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Feed the cat');
    expect(findButtonByAriaLabel(compiled, 'Mark done')).toBeTruthy();
  });

  it('toggles a task\'s completion', async () => {
    const task = occurrence({ itemId: 'task-1', kind: 1, title: 'Feed the cat', startsAt: null, endsAt: null, dueAt: `${today}T17:00:00Z` });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByAriaLabel(compiled, 'Mark done')?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true, null);
    expect(findButtonByAriaLabel(compiled, 'Mark not done')).toBeTruthy();
  });

  it('disables the completion toggle for a task due on a future day', async () => {
    const tomorrow = addDays(today, 1);
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Feed the cat',
      startsAt: null,
      endsAt: null,
      dueAt: `${tomorrow}T17:00:00Z`
    });

    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const button = findButtonByAriaLabel(compiled, 'Mark done');
    expect(button?.disabled).toBe(true);

    button?.click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).not.toHaveBeenCalled();
  });

  it('renders "All day" instead of a time range for an all-day occurrence', async () => {
    const allDay = occurrence({ itemId: 'all-day', title: 'Field trip', isAllDay: true });
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [allDay]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('All day');
  });

  it('shows no filter checkboxes when only one calendar is accessible', async () => {
    const { fixture } = await setup();
    await settle(fixture);
    expect((fixture.nativeElement as HTMLElement).querySelector('input[type="checkbox"]')).toBeFalsy();
  });

  it('shows a per-calendar filter when more than one calendar is accessible', async () => {
    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [calendarSummary(), calendarSummary({ id: 'cal-2', name: 'School' })]) }
    });
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('School');
  });

  it('hides occurrences for a calendar toggled off', async () => {
    const first = occurrence({ itemId: 'a', title: 'Home item', calendarId: 'cal-1', calendarName: 'Home' });
    const second = occurrence({ itemId: 'b', title: 'School item', calendarId: 'cal-2', calendarName: 'School' });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendarSummary(), calendarSummary({ id: 'cal-2', name: 'School' })]),
        listOccurrencesInRange: vi.fn(async () => [first, second])
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const schoolCheckbox = Array.from(compiled.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')).find((checkbox) =>
      checkbox.closest('label')?.textContent?.includes('School')
    )!;

    schoolCheckbox.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Home item');
    expect(compiled.textContent).not.toContain('School item');
  });

  it('never renders create, edit, or delete controls', async () => {
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [occurrence()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('form')).toBeFalsy();
    expect(compiled.querySelector('input[type="text"]')).toBeFalsy();
    expect(compiled.querySelector('input[type="color"]')).toBeFalsy();
  });

  // ----- Template-scheduled task runs (grouped rendering + the compound-key fix) -----

  it('renders a 3-subtask run as one block showing the parent title once, with a toggle per subtask', async () => {
    const subtasks = [
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-1', parentTitle: 'Morning routine', title: 'Brush teeth', startsAt: null, endsAt: null, dueAt: `${today}T08:00:00Z` }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-2', parentTitle: 'Morning routine', title: 'Get dressed', startsAt: null, endsAt: null, dueAt: `${today}T08:10:00Z` }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-3', parentTitle: 'Morning routine', title: 'Eat breakfast', startsAt: null, endsAt: null, dueAt: `${today}T08:20:00Z` })
    ];

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => subtasks) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent?.match(/Morning routine/g)?.length).toBe(1);
    expect(compiled.textContent).toContain('Brush teeth');
    expect(compiled.textContent).toContain('Get dressed');
    expect(compiled.textContent).toContain('Eat breakfast');
    expect(compiled.querySelectorAll('ul.ml-2 button')).toHaveLength(3);
  });

  it('completing one subtask of a 3-subtask run does not flip the other subtasks (the compound-key fix)', async () => {
    const subtasks = [
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-1', parentTitle: 'Morning routine', title: 'Brush teeth', startsAt: null, endsAt: null, dueAt: `${today}T08:00:00Z` }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-2', parentTitle: 'Morning routine', title: 'Get dressed', startsAt: null, endsAt: null, dueAt: `${today}T08:10:00Z` }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-3', parentTitle: 'Morning routine', title: 'Eat breakfast', startsAt: null, endsAt: null, dueAt: `${today}T08:20:00Z` })
    ];

    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => subtasks) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const toggles = Array.from(compiled.querySelectorAll<HTMLButtonElement>('ul.ml-2 button'));
    expect(toggles).toHaveLength(3);

    toggles[0].click();
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'run-1', today, true, 'sub-1');

    const afterToggle = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('ul.ml-2 button'));
    expect(afterToggle[0].textContent?.trim()).toBe('✓');
    // The sibling subtasks must remain untouched -- without the compound (itemId + subtaskId) key,
    // every occurrence sharing itemId "run-1" would have been optimistically flipped too.
    expect(afterToggle[1].textContent?.trim()).toBe('');
    expect(afterToggle[2].textContent?.trim()).toBe('');
  });

  it('navigates the visible week forward and backward', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(compiled.querySelectorAll('button'));
    buttons.find((button) => button.textContent?.includes('Next week'))?.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(addDays(today, 7), addDays(today, 13));

    buttons.find((button) => button.textContent?.includes('Previous week'))?.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(today, addDays(today, 6));
  });
});
