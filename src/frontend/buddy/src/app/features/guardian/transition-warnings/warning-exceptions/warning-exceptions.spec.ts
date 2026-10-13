import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { nestOccurrence } from '../../../../../testing/occurrence-fixture';
import { CalendarOccurrence, CalendarsService } from '../../../../core/calendars.service';
import {
  ChildTransitionWarnings,
  ItemWarningOverrideDetail,
  TransitionWarningsService,
} from '../../../../core/transition-warnings.service';
import { WarningExceptions } from './warning-exceptions';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function occurrence(
  itemId: string,
  title: string,
  fields: Partial<CalendarOccurrence> & { subtaskId?: string; parentTitle?: string } = {},
): CalendarOccurrence {
  return nestOccurrence<CalendarOccurrence>({
    itemId,
    kind: 'Event',
    title,
    icon: '📅',
    iconOverride: null,
    color: '#0f0',
    startsAt: '2026-10-14T14:00:00Z',
    isAllDay: false,
    isCompleted: false,
    createdBy: 'g',
    lastModifiedBy: 'g',
    assignedTo: null,
    calendarId: 'cal',
    calendarName: 'Family',
    ...fields,
  } as never);
}

const FOOTBALL: ItemWarningOverrideDetail = {
  itemId: 'football',
  title: 'Football',
  icon: '⚽',
  kind: 'Event',
  leadMinutes: [30, 10],
};

describe('WarningExceptions', () => {
  afterEach(() => vi.useRealTimers());

  async function setup(overrides: ItemWarningOverrideDetail[] = []) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 13, 9, 0));
    const updated = { childId: 'c1', overrides: [] } as unknown as ChildTransitionWarnings;
    const api = {
      setOverride: vi.fn(async () => updated),
      clearOverride: vi.fn(async () => undefined),
    };
    const calendars = {
      listOccurrencesInRange: vi.fn(async () => [
        occurrence('football', 'Football'),
        occurrence('football', 'Football'),
        occurrence('swim', 'Swimming'),
        occurrence('bag', 'Pack your bag', { kind: 'Task', assignedTo: 'c1' }),
        occurrence('sibling', 'Sibling chore', { kind: 'Task', assignedTo: 'c2' }),
        occurrence('bed', 'Brush teeth', {
          kind: 'Task',
          assignedTo: 'c1',
          subtaskId: 's1',
          parentTitle: 'Go to bed',
        }),
      ]),
    };
    await TestBed.configureTestingModule({
      imports: [WarningExceptions],
      providers: [
        { provide: TransitionWarningsService, useValue: api },
        { provide: CalendarsService, useValue: calendars },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(WarningExceptions);
    fixture.componentRef.setInput('childId', 'c1');
    fixture.componentRef.setInput('childName', 'Emil');
    fixture.componentRef.setInput('overrides', overrides);
    const saved = vi.fn();
    const removed = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.componentInstance.removed.subscribe(removed);
    await settle(fixture);
    return {
      fixture,
      element: fixture.nativeElement as HTMLElement,
      api,
      calendars,
      saved,
      removed,
    };
  }

  function button(element: HTMLElement, text: string): HTMLButtonElement {
    return [...element.querySelectorAll('button')].find(
      (b) => b.textContent?.trim() === text,
    ) as HTMLButtonElement;
  }

  it('lists the exceptions with their own times or "No heads-up"', async () => {
    const { element, calendars } = await setup([
      FOOTBALL,
      { ...FOOTBALL, itemId: 'bag', title: 'Pack your bag', leadMinutes: [] },
    ]);

    const rows = [...element.querySelectorAll('li')].map((li) => li.textContent);
    expect(rows[0]).toContain('Football');
    expect(rows[0]).toContain('30 min, 10 min');
    expect(rows[1]).toContain('No heads-up');
    // The child's items are only asked for once "Add exception" opens.
    expect(calendars.listOccurrencesInRange).not.toHaveBeenCalled();
  });

  it('says so when there are none', async () => {
    const { element } = await setup();
    expect(element.textContent).toContain('No exceptions yet.');
  });

  it("offers the next 14 days' events and the child's own tasks, once each, minus existing exceptions", async () => {
    const { fixture, element, calendars } = await setup([FOOTBALL]);

    button(element, 'Add exception').click();
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith('2026-10-13', '2026-10-26');
    const options = [...element.querySelectorAll('option')].map((o) => o.textContent?.trim());
    expect(options).toEqual(['Choose an item…', '📅 Go to bed', '📅 Pack your bag', '📅 Swimming']);
  });

  it('saves an item with its own lead times', async () => {
    const { fixture, element, api, saved } = await setup();
    button(element, 'Add exception').click();
    await settle(fixture);

    const select = element.querySelector('select') as HTMLSelectElement;
    select.value = 'swim';
    select.dispatchEvent(new Event('change'));
    button(element, '30 min').click();
    await settle(fixture);
    button(element, 'Save exception').click();
    await settle(fixture);

    expect(api.setOverride).toHaveBeenCalledWith('c1', 'swim', [30, 10]);
    expect(saved).toHaveBeenCalled();
    expect(element.querySelector('select')).toBeNull();
  });

  it('saves "No heads-up" as an empty list', async () => {
    const { fixture, element, api } = await setup();
    button(element, 'Add exception').click();
    await settle(fixture);

    const select = element.querySelector('select') as HTMLSelectElement;
    select.value = 'bag';
    select.dispatchEvent(new Event('change'));
    (element.querySelector('button[role="switch"]') as HTMLButtonElement).click();
    await settle(fixture);
    expect(element.querySelector('app-lead-chips')).toBeNull();
    button(element, 'Save exception').click();
    await settle(fixture);

    expect(api.setOverride).toHaveBeenCalledWith('c1', 'bag', []);
  });

  it('explains the cap when the server answers too_many_warning_overrides', async () => {
    const { fixture, element, api } = await setup();
    api.setOverride.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 409, error: { code: 'too_many_warning_overrides' } }),
    );
    button(element, 'Add exception').click();
    await settle(fixture);
    const select = element.querySelector('select') as HTMLSelectElement;
    select.value = 'swim';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);
    button(element, 'Save exception').click();
    await settle(fixture);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'at most 100 exceptions',
    );
  });

  it('removes an exception', async () => {
    const { fixture, element, api, removed } = await setup([FOOTBALL]);

    (
      element.querySelector('[aria-label="Remove the exception for Football"]') as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(api.clearOverride).toHaveBeenCalledWith('c1', 'football');
    expect(removed).toHaveBeenCalledWith('football');
  });
});
