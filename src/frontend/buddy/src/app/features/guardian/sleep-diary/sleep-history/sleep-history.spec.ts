import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { SleepEntry } from '../../../../core/sleep-diary.service';
import { SleepHistory } from './sleep-history';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}

const entry: SleepEntry = {
  date: '2026-03-03',
  isWeekend: false,
  loggedBy: 'g',
  routineStartTime: null,
  ritualStartTime: null,
  ritualEndTime: null,
  bedTime: '20:15',
  fellAsleepTime: '20:45',
  nightWakeUps: [
    { startTime: '03:30', durationMinutes: 30 },
    { startTime: '05:00', durationMinutes: 5 },
  ],
  morningWakeTime: '06:30',
  isTired: true,
  naps: [],
  totalSleepMinutes: 555,
  remarks: '',
};

describe('SleepHistory', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [SleepHistory] }).compileComponents();

    const fixture = TestBed.createComponent(SleepHistory);
    fixture.componentRef.setInput('days', ['2026-03-02', '2026-03-03']);
    fixture.componentRef.setInput('entries', { '2026-03-03': entry });
    fixture.componentRef.setInput('selectedDate', '2026-03-03');
    const selectDate = vi.fn();
    fixture.componentInstance.selectDate.subscribe(selectDate);
    await settle(fixture);

    const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr'));
    return { rows, selectDate };
  }

  it('lists every night newest first, with blanks for nights not logged', async () => {
    const { rows } = await setup();

    expect(rows).toHaveLength(2);
    expect(rows[0].querySelector('th')?.textContent).toContain('Mar 3');
    expect(rows[0].textContent).toContain('8:15');
    expect(rows[0].textContent).toContain('9 h 15 min');
    expect(rows[0].textContent).toContain('Yes');
    expect(rows[0].querySelectorAll('td')[2].textContent?.trim()).toBe('2');
    expect(rows[0].getAttribute('aria-current')).toBe('date');
    expect(rows[1].textContent).toContain('Not logged');
    expect(rows[1].getAttribute('aria-current')).toBeNull();
  });

  it('emits the night chosen for editing', async () => {
    const { rows, selectDate } = await setup();

    (rows[1].querySelector('button') as HTMLButtonElement).click();

    expect(selectDate).toHaveBeenCalledWith('2026-03-02');
  });
});
