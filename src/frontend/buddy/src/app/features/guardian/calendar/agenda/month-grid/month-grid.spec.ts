import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { CalendarOccurrence } from '../../../../../core/calendars.service';
import { AgendaDay } from '../agenda';
import { MonthGrid } from './month-grid';

describe('MonthGrid', () => {
  function day(date: string, isCurrentMonth = true): AgendaDay {
    return { date, label: String(Number(date.slice(-2))), isCurrentMonth };
  }

  function occurrence(overrides: Partial<CalendarOccurrence> = {}): CalendarOccurrence {
    return {
      itemId: 'item-1',
      kind: 0,
      title: 'Dentist',
      icon: '🦷',
      iconOverride: null,
      color: '#112233',
      startsAt: '2024-06-10T09:00:00Z',
      endsAt: '2024-06-10T10:00:00Z',
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

  async function setup(inputs: {
    days: AgendaDay[];
    weekdayLabels?: readonly string[];
    occurrencesByDate?: Record<string, CalendarOccurrence[]>;
    today?: string;
  }) {
    await TestBed.configureTestingModule({ imports: [MonthGrid] }).compileComponents();

    const fixture = TestBed.createComponent(MonthGrid);
    fixture.componentRef.setInput('days', inputs.days);
    fixture.componentRef.setInput('weekdayLabels', inputs.weekdayLabels ?? ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']);
    fixture.componentRef.setInput('occurrencesByDate', inputs.occurrencesByDate ?? {});
    fixture.componentRef.setInput('today', inputs.today ?? '2024-06-15');
    fixture.detectChanges();

    return { fixture };
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  it('lays out 7 days per row across as many rows as it is given', async () => {
    const days = Array.from({ length: 35 }, (_, index) => day(`2024-05-${String(27 + index).padStart(2, '0')}`));
    // Only the first 5 (2024-05-27..31) are real dates in May -- the rest wrap via padStart concat,
    // which is fine here since this test only checks the grid's row/column shape, not real dates.
    const { fixture } = await setup({ days });

    const compiled = fixture.nativeElement as HTMLElement;
    const cells = compiled.querySelectorAll('button');
    expect(cells).toHaveLength(35);
  });

  it('renders the weekday header labels in order', async () => {
    const { fixture } = await setup({ days: [day('2024-06-10')] });

    const compiled = fixture.nativeElement as HTMLElement;
    const headers = Array.from(compiled.querySelectorAll('div.bg-slate-50')).map((el) => el.textContent?.trim());
    expect(headers).toEqual(['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']);
  });

  it('shows up to 3 occurrence chips and a "+N more" label for the rest', async () => {
    const occurrences = Array.from({ length: 5 }, (_, index) => occurrence({ itemId: `item-${index}`, title: `Event ${index}` }));
    const { fixture } = await setup({
      days: [day('2024-06-10')],
      occurrencesByDate: { '2024-06-10': occurrences }
    });

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Event 0');
    expect(compiled.textContent).toContain('Event 1');
    expect(compiled.textContent).toContain('Event 2');
    expect(compiled.textContent).not.toContain('Event 3');
    expect(compiled.textContent).toContain('+2 more');
  });

  it('shows no "+N more" label when every occurrence fits within the cap', async () => {
    const occurrences = [occurrence({ itemId: 'item-1', title: 'Only event' })];
    const { fixture } = await setup({
      days: [day('2024-06-10')],
      occurrencesByDate: { '2024-06-10': occurrences }
    });

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('more');
  });

  it('collapses every subtask occurrence of one template-scheduled run into a single chip', async () => {
    const subtasks = [
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-1', parentTitle: 'Morning routine', title: 'Brush teeth' }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-2', parentTitle: 'Morning routine', title: 'Get dressed' }),
      occurrence({ itemId: 'run-1', kind: 1, subtaskId: 'sub-3', parentTitle: 'Morning routine', title: 'Eat breakfast' })
    ];

    const { fixture } = await setup({
      days: [day('2024-06-10')],
      occurrencesByDate: { '2024-06-10': subtasks }
    });

    const compiled = fixture.nativeElement as HTMLElement;
    // Without grouping, three chips sharing an itemId would also collide on Angular's @for track
    // key -- grouping collapses them to one chip showing the run's own (parent) title.
    expect(compiled.textContent).toContain('Morning routine');
    expect(compiled.textContent).not.toContain('Brush teeth');
    expect(compiled.textContent).not.toContain('more');
  });

  it('emits daySelected with the clicked day\'s date', async () => {
    const { fixture } = await setup({ days: [day('2024-06-10'), day('2024-06-11')] });
    const emitted: string[] = [];
    fixture.componentInstance.daySelected.subscribe((date: string) => emitted.push(date));

    findButtonByText(fixture.nativeElement as HTMLElement, '11')!.click();

    expect(emitted).toEqual(['2024-06-11']);
  });
});
