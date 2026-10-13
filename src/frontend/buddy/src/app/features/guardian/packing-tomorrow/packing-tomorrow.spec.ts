import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { CalendarOccurrence, CalendarsService } from '../../../core/calendars.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { settle } from '../../../../testing/onboarding-fixture';
import { nestOccurrence } from '../../../../testing/occurrence-fixture';
import { PackingTomorrow } from './packing-tomorrow';

describe('PackingTomorrow', () => {
  const evening = new Date(2026, 9, 13, 20, 0);
  const afternoon = new Date(2026, 9, 13, 16, 59);
  const tomorrow = '2026-10-14';

  function child(id: string, givenName: string): ChildSummary {
    return {
      showVisualTimers: true,
      id,
      name: { givenName, familyName: 'Kid' },
      guardianLinkId: `link-${id}`,
      kind: 'Parent',
      language: 'en',
      timeZoneId: 'UTC',
    };
  }

  function item(
    itemId: string,
    subtaskId: string,
    assignedTo: string | null,
    isCompleted = false,
    eveningBeforeFrom: string | null = '17:00:00',
  ): CalendarOccurrence {
    return nestOccurrence<CalendarOccurrence>({
      itemId,
      kind: 'Task',
      title: subtaskId,
      icon: '🎒',
      iconOverride: null,
      color: '#000',
      isAllDay: false,
      isCompleted,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      assignedTo,
      calendarId: 'cal-1',
      calendarName: 'Home',
      timing: { kind: 1, dueAt: `${tomorrow}T05:45:00Z` },
      routine: {
        subtaskId,
        parentTitle: 'School bag',
        parentIcon: '🎒',
        checklist: { eveningBeforeFrom },
      },
    });
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(at: Date, occurrences: CalendarOccurrence[]) {
    vi.setSystemTime(at);
    const calendars = { listOccurrencesInRange: vi.fn(async () => occurrences) };
    const guardians = {
      listMyChildren: vi.fn(async () => [child('emil', 'Emil'), child('ida', 'Ida')]),
    };

    await TestBed.configureTestingModule({
      imports: [PackingTomorrow],
      providers: [
        provideRouter([]),
        { provide: CalendarsService, useValue: calendars },
        { provide: GuardiansService, useValue: guardians },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(PackingTomorrow);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, calendars };
  }

  it("lists each child's unpacked list for tomorrow after its evening time, linking to the library", async () => {
    const { compiled, calendars } = await setup(evening, [
      item('bag-emil', 'lunchbox', 'emil', true),
      item('bag-emil', 'gym', 'emil'),
      item('bag-emil', 'glasses', 'emil'),
      item('bag-ida', 'lunchbox', 'ida'),
    ]);

    expect(calendars.listOccurrencesInRange).toHaveBeenCalledWith(tomorrow, tomorrow);
    const card = compiled.querySelector('[data-testid="packing-tomorrow"]')!;
    expect(card.textContent).toContain('Packing for tomorrow');
    // Whole in one dashboard column, spaced like the other cards.
    expect([...card.classList]).toEqual(
      expect.arrayContaining(['mb-6', 'block', 'break-inside-avoid']),
    );
    const rows = Array.from(card.querySelectorAll('a')).map((a) =>
      a.textContent!.replace(/\s+/g, ' ').trim(),
    );
    expect(rows).toEqual([
      '🎒 Emil hasn’t packed for tomorrow: 1 of 3 items · School bag',
      '🎒 Ida hasn’t packed for tomorrow: 0 of 1 items · School bag',
    ]);
    expect(card.querySelector('a')!.getAttribute('href')).toBe('/guardian/task-library?child=emil');
  });

  it('loads the new tomorrow when the dashboard stays open past midnight', async () => {
    const { fixture, calendars } = await setup(evening, []);

    vi.setSystemTime(new Date(2026, 9, 14, 0, 1));
    fixture.componentInstance['now'].set(Date.now());
    await settle(fixture);

    expect(calendars.listOccurrencesInRange).toHaveBeenLastCalledWith('2026-10-15', '2026-10-15');
  });

  it('is hidden before the evening time', async () => {
    const { compiled } = await setup(afternoon, [item('bag-emil', 'gym', 'emil')]);

    expect(compiled.querySelector('[data-testid="packing-tomorrow"]')).toBeNull();
  });

  it('leaves out packed lists, day-only lists and lists of someone else’s child', async () => {
    const { compiled } = await setup(evening, [
      item('packed', 'lunchbox', 'emil', true),
      item('day-only', 'lunchbox', 'emil', false, null),
      item('stranger', 'lunchbox', 'someone-else'),
      item('unassigned', 'lunchbox', null),
    ]);

    expect(compiled.querySelector('[data-testid="packing-tomorrow"]')).toBeNull();
  });
});
