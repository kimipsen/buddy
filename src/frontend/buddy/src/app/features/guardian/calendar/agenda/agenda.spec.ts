import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  AssignableMember,
  CalendarOccurrence,
  CalendarSummary,
  CalendarsService
} from '../../../../core/calendars.service';
import { toIsoDate, todayIsoDate } from '../../../../core/date-utils';
import { UsersService } from '../../../../core/users.service';
import { CalendarAgenda } from './agenda';

describe('CalendarAgenda', () => {
  const today = todayIsoDate();

  // Mirrors agenda.ts's own local-date arithmetic (parseIsoDate + toIsoDate) so expectations don't
  // depend on the host machine's time zone -- both this helper and the component read local Date
  // components only, never a UTC-parsed "YYYY-MM-DD".
  function addDays(isoDate: string, days: number): string {
    const [year, month, day] = isoDate.split('-').map(Number);
    return toIsoDate(new Date(year, month - 1, day + days));
  }

  function calendarSummary(overrides: Partial<CalendarSummary> = {}): CalendarSummary {
    return { id: 'cal-1', name: 'Home', icon: '🏠', role: 0, ...overrides };
  }

  // startsAt/dueAt are given as UTC instants ("...Z") whose date component is exactly the intended
  // local calendar day. Since occurrencesByDate groups by toIsoDateInTimeZone(instant, 'UTC') (the
  // UsersService stub below fixes timeZoneId to 'UTC'), and buildDays' day.date strings never go
  // through any time zone conversion, this keeps fixtures and day buckets aligned everywhere.
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
      listAssignableMembers: vi.fn(async () => []),
      setTaskCompletion: vi.fn(async () => ({ itemId: 'item-1', occurrenceDate: today, isCompleted: true })),
      deleteItem: vi.fn(async () => undefined),
      updateItemDetails: vi.fn(async () => ({}) as never),
      rescheduleItem: vi.fn(async () => ({}) as never),
      createItem: vi.fn(async () => ({}) as never),
      ...stubs.calendars
    };

    await TestBed.configureTestingModule({
      imports: [CalendarAgenda],
      providers: [
        { provide: UsersService, useValue: usersStub },
        { provide: CalendarsService, useValue: calendarsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CalendarAgenda);

    return { fixture, calendars: calendarsStub };
  }

  // loadWeek chains a Promise.all, and a successful load can itself trigger the newCalendarId
  // effect (auto-picking the first eligible calendar), which fires a second async call
  // (loadAssignableMembers) before the DOM reflects its result -- a single detectChanges()/flush
  // isn't reliably enough, so flush the macrotask queue a handful of times (per docs/testing.md).
  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  // For a plain string-bound select ([value]="x.id"), the rendered option.value equals the bound
  // string directly.
  function selectValue(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  // For an [ngValue]-bound select (numbers/null), Angular encodes option.value internally -- read
  // the real rendered value back off the option at the given index rather than guessing the
  // encoding (same approach as manage-groups.spec.ts).
  function selectByIndex(select: HTMLSelectElement, index: number): void {
    const options = select.querySelectorAll('option');
    select.value = options[index].value;
    select.dispatchEvent(new Event('change'));
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  // The create form and an open edit form can both be present at once (they aren't mutually
  // exclusive), so `form` alone isn't a reliable index -- anchor on the create form's own title
  // input instead.
  function createForm(compiled: HTMLElement): HTMLFormElement {
    return compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!.closest('form')!;
  }

  // ----- Loading / empty / error -----

  it('shows the loading message before the week resolves', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading your calendars…');
  });

  it('shows the empty state once loading finishes with no occurrences', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Loading your calendars…');
    expect(compiled.textContent).toContain('Nothing planned this week.');
  });

  it('shows the translated error message when listMyCalendars rejects', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load your calendars.');
  });

  it('shows the translated error message when listOccurrencesInRange rejects', async () => {
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load your calendars.');
  });

  it('requests the current week (today through six days ahead) on first load', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith(today, addDays(today, 6));
  });

  // ----- Grouping and ordering -----

  it('groups occurrences under the correct day and orders same-day items by time', async () => {
    const tomorrow = addDays(today, 1);
    const early = occurrence({ itemId: 'early', title: 'Early meeting', startsAt: `${today}T08:00:00Z`, endsAt: `${today}T08:30:00Z` });
    const late = occurrence({ itemId: 'late', title: 'Late meeting', startsAt: `${today}T18:00:00Z`, endsAt: `${today}T18:30:00Z` });
    const nextDay = occurrence({ itemId: 'tomorrow', title: 'Tomorrow item', startsAt: `${tomorrow}T09:00:00Z`, endsAt: `${tomorrow}T09:30:00Z` });

    // Deliberately out of order to prove the component sorts, not just echoes fixture order.
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [late, nextDay, early]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const text = compiled.textContent ?? '';
    expect(text.indexOf('Early meeting')).toBeLessThan(text.indexOf('Late meeting'));
    expect(text.indexOf('Late meeting')).toBeLessThan(text.indexOf('Tomorrow item'));
  });

  it('groups a task by its due date, not its (absent) start date', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Buy groceries',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`
    });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Buy groceries');
    expect(compiled.textContent).not.toContain('Nothing planned this week.');
  });

  it('omits an occurrence with neither startsAt nor dueAt from every day bucket', async () => {
    const broken = occurrence({ itemId: 'broken', title: 'Ghost item', startsAt: null, endsAt: null, dueAt: null });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [broken]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing planned this week.');
    expect(compiled.textContent).not.toContain('Ghost item');
  });

  it('renders "All day" instead of a time range for an all-day occurrence', async () => {
    const allDay = occurrence({ itemId: 'all-day', title: 'Field trip', isAllDay: true });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [allDay]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('All day');
  });

  // ----- Navigation -----

  it('requests the previous week and shifts every displayed day back seven days', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    findButtonByText(fixture.nativeElement as HTMLElement, 'Previous week')!.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(addDays(today, -7), addDays(today, -1));
  });

  it('requests the next week and shifts every displayed day forward seven days', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    findButtonByText(fixture.nativeElement as HTMLElement, 'Next week')!.click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith(addDays(today, 7), addDays(today, 13));
  });

  it('an occurrence outside the visible week is not shown even though it was returned', async () => {
    // The stub ignores from/to and always returns the same fixed-today occurrence -- once the
    // component has navigated away to next week, that occurrence's date no longer falls in any of
    // the seven displayed days, so it must be filtered out of every bucket.
    const fixedToday = occurrence({ itemId: 'today-item', title: 'Today only' });
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [fixedToday]) } });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Today only');

    findButtonByText(fixture.nativeElement as HTMLElement, 'Next week')!.click();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Today only');
  });

  it('shows the empty state when occurrences() holds only out-of-range items (e.g. stale data after navigating)', async () => {
    const fixedToday = occurrence({ itemId: 'today-item', title: 'Today only' });
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [fixedToday]) } });
    await settle(fixture);

    findButtonByText(fixture.nativeElement as HTMLElement, 'Next week')!.click();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Today only');
    expect(compiled.textContent).toContain('Nothing planned this week.');
  });

  // ----- Calendar visibility filter -----

  it('does not show the calendar filter when the guardian has only one calendar', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendarSummary()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Calendars');
  });

  it('shows a filter checkbox per calendar once there is more than one, and hiding one filters its items', async () => {
    const home = calendarSummary({ id: 'cal-1', name: 'Home' });
    const school = calendarSummary({ id: 'cal-2', name: 'School' });
    const homeItem = occurrence({ itemId: 'home-item', title: 'Home item', calendarId: 'cal-1', calendarName: 'Home' });
    const schoolItem = occurrence({ itemId: 'school-item', title: 'School item', calendarId: 'cal-2', calendarName: 'School' });

    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [home, school]),
        listOccurrencesInRange: vi.fn(async () => [homeItem, schoolItem])
      }
    });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Home item');
    expect(compiled.textContent).toContain('School item');

    const checkboxes = Array.from(compiled.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'));
    const homeCheckbox = checkboxes.find((checkbox) => checkbox.parentElement?.textContent?.includes('Home'))!;
    expect(homeCheckbox.checked).toBe(true);

    homeCheckbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Home item');
    expect(compiled.textContent).toContain('School item');
  });

  it('shows the empty state once the only visible calendar is hidden, even though occurrences() is non-empty', async () => {
    const { fixture } = await setup({
      calendars: {
        listMyCalendars: vi.fn(async () => [calendarSummary({ id: 'cal-1' }), calendarSummary({ id: 'cal-2', name: 'School' })]),
        listOccurrencesInRange: vi.fn(async () => [occurrence({ calendarId: 'cal-1' })])
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const homeCheckbox = compiled.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    homeCheckbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Nothing planned this week.');
  });

  // ----- Task completion toggle -----

  it('toggles an incomplete task to complete with the exact calendar/item/date/completion args', async () => {
    const task = occurrence({ itemId: 'task-1', kind: 1, title: 'Feed cat', startsAt: null, endsAt: null, dueAt: `${today}T09:00:00Z` });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const checkbox = compiled.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    expect(checkbox.checked).toBe(false);

    checkbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(calendars.setTaskCompletion).toHaveBeenCalledWith('cal-1', 'task-1', today, true);
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[type="checkbox"]')!.checked).toBe(true);
  });

  it('shows an error and leaves completion unchanged when toggling a task fails', async () => {
    const task = occurrence({ itemId: 'task-1', kind: 1, title: 'Feed cat', startsAt: null, endsAt: null, dueAt: `${today}T09:00:00Z` });
    const { fixture } = await setup({
      calendars: {
        listOccurrencesInRange: vi.fn(async () => [task]),
        setTaskCompletion: vi.fn(async () => Promise.reject(new Error('boom')))
      }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector<HTMLInputElement>('input[type="checkbox"]')!.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Unable to update this task.');
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[type="checkbox"]')!.checked).toBe(false);
  });

  // ----- Delete -----

  it('asks for confirmation before deleting, and cancelling makes no service call', async () => {
    const item = occurrence({ itemId: 'item-1', title: 'Dentist' });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Delete this?');

    findButtonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(calendars.deleteItem).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Delete this?');
  });

  it('deletes the item with the exact calendar/item ids and removes it from the list without reloading', async () => {
    const item = occurrence({ itemId: 'item-1', title: 'Dentist', calendarId: 'cal-1' });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(calendars.deleteItem).toHaveBeenCalledWith('cal-1', 'item-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Nothing planned this week.');
    // Deletion updates the occurrences signal in place -- it must not re-fetch the week.
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledTimes(1);
  });

  it('shows an error and keeps the item when deletion fails', async () => {
    const item = occurrence({ itemId: 'item-1', title: 'Dentist' });
    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [item]), deleteItem: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Delete')!.click();
    await settle(fixture);
    compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to delete this item.');
    expect(compiled.textContent).toContain('Dentist');
  });

  // ----- Edit -----

  it('prefills the edit form from the occurrence and saves details + reschedule with exact args, then reloads', async () => {
    const item = occurrence({
      itemId: 'item-1',
      title: 'Dentist',
      color: '#abcdef',
      calendarId: 'cal-1',
      startsAt: `${today}T09:00:00Z`,
      endsAt: `${today}T10:00:00Z`,
      isAllDay: false
    });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Edit')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    const titleInput = compiled.querySelector<HTMLInputElement>('input[name="editTitle"]')!;
    expect(titleInput.value).toBe('Dentist');

    setInputValue(titleInput, 'Dentist checkup');
    await settle(fixture);

    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.updateItemDetails).toHaveBeenCalledWith('cal-1', 'item-1', { title: 'Dentist checkup', icon: null, color: '#abcdef' });
    expect(calendars.rescheduleItem).toHaveBeenCalledWith('cal-1', 'item-1', {
      startsAt: { date: today, time: '09:00:00' },
      endsAt: { date: today, time: '10:00:00' },
      dueDate: null,
      isAllDay: false
    });
    // Success closes the edit form and reloads the week (initial load + this reload).
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledTimes(2);
    expect((fixture.nativeElement as HTMLElement).querySelector('input[name="editTitle"]')).toBeNull();
  });

  it('stores an all-day event reschedule with a sentinel 00:00 time and an exclusive end date one day later', async () => {
    const item = occurrence({
      itemId: 'item-1',
      title: 'Field trip',
      calendarId: 'cal-1',
      isAllDay: true,
      startsAt: `${today}T00:00:00Z`,
      endsAt: `${addDays(today, 1)}T00:00:00Z` // stored exclusive: inclusive display day is `today`
    });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Edit')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.rescheduleItem).toHaveBeenCalledWith('cal-1', 'item-1', {
      startsAt: { date: today, time: '00:00:00' },
      endsAt: { date: addDays(today, 1), time: '00:00:00' },
      dueDate: null,
      isAllDay: true
    });
  });

  it('reschedules a task by its due date, leaving startsAt/endsAt null', async () => {
    const item = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Buy groceries',
      calendarId: 'cal-1',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T17:00:00Z`,
      isAllDay: false
    });
    const { fixture, calendars } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Edit')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.rescheduleItem).toHaveBeenCalledWith('cal-1', 'task-1', {
      startsAt: null,
      endsAt: null,
      dueDate: { date: today, time: '17:00:00' },
      isAllDay: false
    });
  });

  it('shows an edit error and keeps the form open (does not reload) when saving fails', async () => {
    const item = occurrence({ itemId: 'item-1', title: 'Dentist' });
    const { fixture, calendars } = await setup({
      calendars: {
        listOccurrencesInRange: vi.fn(async () => [item]),
        updateItemDetails: vi.fn(async () => Promise.reject(new Error('boom')))
      }
    });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Edit')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to update this item. Check the details and try again.');
    expect(compiled.querySelector('input[name="editTitle"]')).not.toBeNull();
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledTimes(1);
  });

  it('disables the Save button while the edit title is blank', async () => {
    const item = occurrence({ itemId: 'item-1', title: 'Dentist' });
    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [item]) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Edit')!.click();
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    const titleInput = compiled.querySelector<HTMLInputElement>('input[name="editTitle"]')!;
    setInputValue(titleInput, '   ');
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);
  });

  // ----- Create -----

  it('disables the add button until a title is entered, given a valid default calendar/kind/dates', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Add to calendar')!.disabled).toBe(true);

    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, 'Piano lesson');
    await settle(fixture);

    expect(findButtonByText(fixture.nativeElement as HTMLElement, 'Add to calendar')!.disabled).toBe(false);
  });

  it('creates a timed event with the exact request the service expects', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, '  Piano lesson  ');
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createItem).toHaveBeenCalledWith('cal-1', {
      kind: 0,
      title: 'Piano lesson',
      icon: null,
      color: '#f43f5e',
      startsAt: { date: today, time: '09:00:00' },
      endsAt: { date: today, time: '10:00:00' },
      dueDate: null,
      isAllDay: false,
      recurrence: null,
      assignedTo: null
    });
  });

  it('creates an all-day task assigned to a member, with a null time sentinel and the picked assignee', async () => {
    const members: AssignableMember[] = [{ userId: 'child-1', givenName: 'Sam', familyName: 'Kid' }];
    const { fixture, calendars } = await setup({ calendars: { listAssignableMembers: vi.fn(async () => members) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    const kindSelect = compiled.querySelector<HTMLSelectElement>('select[name="itemKind"]')!;
    selectByIndex(kindSelect, 1); // Task
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, 'Take out trash');
    compiled.querySelector<HTMLInputElement>('input[type="checkbox"]')!.dispatchEvent(new Event('change')); // all-day on
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    const assigneeSelect = compiled.querySelector<HTMLSelectElement>('select[name="itemAssignee"]')!;
    selectValue(assigneeSelect, 'child-1');
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createItem).toHaveBeenCalledWith('cal-1', {
      kind: 1,
      title: 'Take out trash',
      icon: null,
      color: '#f43f5e',
      startsAt: null,
      endsAt: null,
      dueDate: { date: today, time: '00:00:00' },
      isAllDay: true,
      recurrence: null,
      assignedTo: 'child-1'
    });
  });

  it('creates a recurring event with the selected frequency and default interval', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, 'Trash day');
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    const repeatSelect = compiled.querySelector<HTMLSelectElement>('select[name="itemRepeat"]')!;
    selectByIndex(repeatSelect, 2); // none(0), daily(1), weekly(2)
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    await settle(fixture);

    expect(calendars.createItem).toHaveBeenCalledWith(
      'cal-1',
      expect.objectContaining({ recurrence: { frequency: 1, intervalCount: 1, until: null } })
    );
  });

  it('resets the form and reloads the week after a successful create', async () => {
    const { fixture, calendars } = await setup();
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    const titleInput = compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!;
    setInputValue(titleInput, 'Piano lesson');
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!.value).toBe('');
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledTimes(2);
  });

  it('shows a create error and does not reset the form or reload when creation fails', async () => {
    const { fixture, calendars } = await setup({ calendars: { createItem: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    const titleInput = compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!;
    setInputValue(titleInput, 'Piano lesson');
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to create this event. Check the details and try again.');
    expect(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!.value).toBe('Piano lesson');
    expect(calendars.listOccurrencesInRange).toHaveBeenCalledTimes(1);
  });

  it('disables the add button while a create is in flight, and clears `creating` once it settles', async () => {
    let resolveCreate!: () => void;
    const createItem = vi.fn(
      () =>
        new Promise<never>((resolve) => {
          resolveCreate = resolve as () => void;
        })
    );
    const { fixture } = await setup({ calendars: { createItem } });
    await settle(fixture);

    let compiled = fixture.nativeElement as HTMLElement;
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, 'Piano lesson');
    await settle(fixture);

    createForm(fixture.nativeElement as HTMLElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    // Disabled while creating() is true, independent of canSubmit() (the title is still filled in).
    expect(findButtonByText(compiled, 'Add to calendar')!.disabled).toBe(true);

    resolveCreate();
    await settle(fixture);

    // A successful create resets the form (title clears), which alone makes canSubmit() false --
    // so re-type a title to isolate whether `creating` itself was actually cleared in `finally`.
    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!.value).toBe('');
    setInputValue(compiled.querySelector<HTMLInputElement>('input[name="itemTitle"]')!, 'Ballet class');
    await settle(fixture);

    compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Add to calendar')!.disabled).toBe(false);
  });

  // ----- Eligible calendars (create-permission gating) -----

  it('hides the create form and shows a message when no calendar is eligible for new items', async () => {
    const { fixture } = await setup({ calendars: { listMyCalendars: vi.fn(async () => [calendarSummary({ role: 2 })]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('You need a calendar you can add to. Create one from Calendars in Settings.');
    expect(compiled.querySelector('input[name="itemTitle"]')).toBeNull();
  });

  it('still shows edit/delete controls for an occurrence on a viewer-only calendar (enforcement is left to the backend)', async () => {
    // MAX_CONTRIBUTE_ROLE only gates eligibleCalendars (the create form's calendar picker) -- the
    // agenda list itself renders Edit/Delete for every occurrence regardless of the viewing
    // guardian's role on that occurrence's calendar, relying on the backend (CalendarAuthorization)
    // to reject a contribute action the caller isn't actually allowed to make.
    const viewerOnly = calendarSummary({ id: 'cal-2', role: 2, name: 'Viewer cal' });
    const item = occurrence({ itemId: 'item-1', title: 'Someone else’s event', calendarId: 'cal-2', calendarName: 'Viewer cal' });

    const { fixture } = await setup({
      calendars: { listMyCalendars: vi.fn(async () => [viewerOnly]), listOccurrencesInRange: vi.fn(async () => [item]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(findButtonByText(compiled, 'Edit')).toBeTruthy();
    expect(findButtonByText(compiled, 'Delete')).toBeTruthy();
  });

  // ----- Assignee name resolution -----

  it('resolves and displays the assignee name for a task once assignable members load', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Take out trash',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T09:00:00Z`,
      assignedTo: 'child-1'
    });
    const members: AssignableMember[] = [{ userId: 'child-1', givenName: 'Sam', familyName: 'Kid' }];

    const { fixture } = await setup({
      calendars: { listOccurrencesInRange: vi.fn(async () => [task]), listAssignableMembers: vi.fn(async () => members) }
    });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Sam Kid');
  });

  it('shows no assignee text for a task assigned to a member the guardian cannot resolve', async () => {
    const task = occurrence({
      itemId: 'task-1',
      kind: 1,
      title: 'Take out trash',
      startsAt: null,
      endsAt: null,
      dueAt: `${today}T09:00:00Z`,
      assignedTo: 'unknown-user'
    });

    const { fixture } = await setup({ calendars: { listOccurrencesInRange: vi.fn(async () => [task]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Take out trash');
    expect(compiled.textContent).not.toContain('→');
  });
});
