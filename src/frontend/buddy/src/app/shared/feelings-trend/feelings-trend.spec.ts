import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import type { FeelingCheckIn } from '../../core/feelings.service';
import { FeelingsTrend, axisPercent } from './feelings-trend';

function checkIn(overrides: Partial<FeelingCheckIn>): FeelingCheckIn {
  return {
    id: 'c1',
    zone: 'Green',
    tags: [],
    note: '',
    date: '2026-10-13',
    time: '14:00',
    loggedBy: 'child-1',
    isSelfReport: true,
    recordedAt: '2026-10-13T12:00:00Z',
    ...overrides,
  };
}

describe('FeelingsTrend', () => {
  async function setup(inputs: Record<string, unknown>) {
    await TestBed.configureTestingModule({ imports: [FeelingsTrend] }).compileComponents();
    const fixture = TestBed.createComponent(FeelingsTrend);
    fixture.componentRef.setInput('days', ['2026-10-12', '2026-10-13']);
    fixture.componentRef.setInput('checkIns', []);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  function row(el: HTMLElement, date: string): HTMLElement {
    return el.querySelector(`tr[data-date="${date}"]`) as HTMLElement;
  }

  it('places the axis from 06:00 to 22:00 and clamps outside it', () => {
    expect(axisPercent('06:00')).toBe(0);
    expect(axisPercent('14:00')).toBe(50);
    expect(axisPercent('22:00')).toBe(100);
    expect(axisPercent('03:00')).toBe(0);
    expect(axisPercent('23:30')).toBe(100);
  });

  it('draws the child’s own check-in filled and an observation outlined, with a spoken summary', async () => {
    const { el } = await setup({
      checkIns: [
        checkIn({ id: 'own', zone: 'Yellow', tags: ['Worried', 'School'], time: '14:00' }),
        checkIn({ id: 'seen', zone: 'Red', isSelfReport: false, time: '16:00' }),
      ],
    });

    const marks = row(el, '2026-10-13').querySelectorAll('[data-self-report]');
    expect(marks).toHaveLength(2);
    expect(marks[0].getAttribute('data-self-report')).toBe('true');
    expect(marks[0].className).toContain('bg-amber-400');
    expect((marks[0] as HTMLElement).style.left).toBe('50%');
    expect(marks[1].className).toContain('border-dashed');
    expect(row(el, '2026-10-13').textContent).toContain(
      '14:00, Wound up (yellow): Worried, School, by the child',
    );
    expect(row(el, '2026-10-13').textContent).toContain(
      '16:00, Too much (red), observed by an adult',
    );
    expect(row(el, '2026-10-12').textContent).toContain('No check-ins');
  });

  it('pairs day D with the night of D-1, and leaves the sleep column out when there is no diary', async () => {
    const withSleep = await setup({ sleepByNight: { '2026-10-12': 555 } });
    expect(row(withSleep.el, '2026-10-13').textContent).toContain('9 h 15 min');
    expect(row(withSleep.el, '2026-10-12').textContent).toContain('Not logged');
    expect(withSleep.el.textContent).toContain('Slept the night before');

    TestBed.resetTestingModule();
    const without = await setup({ sleepByNight: null });
    expect(without.el.textContent).not.toContain('Slept the night before');
  });

  it('marks taken doses only when doses are given', async () => {
    const withDoses = await setup({ dosesByDate: { '2026-10-13': ['08:00'] } });
    expect(row(withDoses.el, '2026-10-13').textContent).toContain('Medicine taken at 08:00');
    expect(withDoses.el.textContent).toContain('Line: medicine taken');

    TestBed.resetTestingModule();
    const without = await setup({});
    expect(without.el.textContent).not.toContain('medicine taken');
  });

  it('selects a day, or shows plain dates when not selectable', async () => {
    const { fixture, el } = await setup({ selectedDate: '2026-10-13' });
    const chosen: string[] = [];
    fixture.componentInstance.selectDate.subscribe((date) => chosen.push(date));

    (row(el, '2026-10-12').querySelector('button') as HTMLButtonElement).click();
    expect(chosen).toEqual(['2026-10-12']);
    expect(row(el, '2026-10-13').querySelector('button')?.getAttribute('aria-pressed')).toBe(
      'true',
    );

    TestBed.resetTestingModule();
    const shared = await setup({ selectable: false });
    expect(shared.el.querySelector('tbody button')).toBeNull();
  });
});
