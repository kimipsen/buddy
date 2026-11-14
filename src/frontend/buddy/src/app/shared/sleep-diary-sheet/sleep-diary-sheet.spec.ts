import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { SleepEntry } from '../../core/sleep-diary.service';
import type { NightDutyCell } from '../../features/guardian/sleep-diary/sleep-night-duty';
import { SleepDiarySheet } from './sleep-diary-sheet';

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
  nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
  morningWakeTime: '06:30',
  isTired: true,
  naps: [],
  totalSleepMinutes: 555,
  remarks: 'Restless',
};

describe('SleepDiarySheet', () => {
  async function setup(nightDuty: Partial<Record<string, NightDutyCell>> | null = null) {
    await TestBed.configureTestingModule({ imports: [SleepDiarySheet] }).compileComponents();

    const fixture = TestBed.createComponent(SleepDiarySheet);
    fixture.componentRef.setInput('days', ['2026-03-02', '2026-03-03']);
    fixture.componentRef.setInput('entries', [entry]);
    fixture.componentRef.setInput('hygieneNotes', 'Dim the lights');
    if (nightDuty) {
      fixture.componentRef.setInput('nightDuty', nightDuty);
    }
    await settle(fixture);

    const host = fixture.nativeElement as HTMLElement;
    const headers = Array.from(host.querySelectorAll('thead th')).map((th) =>
      th.textContent?.trim(),
    );
    const rows = Array.from(host.querySelectorAll('tbody tr'));
    return { host, headers, rows };
  }

  it('has the 12 form columns, a row per day and a blank row with 10 empty cells', async () => {
    const { host, headers, rows } = await setup();

    expect(headers).toHaveLength(12);
    expect(headers[0]).toBe('Date');
    expect(headers[2]).toBe('Getting ready');
    expect(headers[11]).toBe('Remarks');
    expect(rows).toHaveLength(2);
    expect(rows[0].querySelectorAll('th, td')).toHaveLength(12);
    expect(rows[0].querySelectorAll('td')).toHaveLength(11);
    const blankCells = Array.from(rows[0].querySelectorAll('td')).slice(1);
    expect(blankCells).toHaveLength(10);
    expect(blankCells.every((td) => td.textContent?.trim() === '')).toBe(true);
    expect(rows[1].textContent).toContain('8:15');
    expect(rows[1].textContent).toContain('9 h 15 min');
    expect(rows[1].textContent).toContain('Restless');
    expect(host.querySelector('[data-testid="night-duty-cell"]')).toBeNull();
    expect(host.textContent).toContain('Dim the lights');
  });

  it('adds a night duty column after Weekend, filled on logged nights only', async () => {
    const { headers, rows } = await setup({
      '2026-03-03': { who: { icon: '👤', text: 'Dad' }, note: 'Screen night' },
      '2026-03-02': { who: { icon: '👤', text: 'Mom' }, note: '' },
    });

    expect(headers).toHaveLength(13);
    expect(headers[1]).toBe('Weekend');
    expect(headers[2]).toBe('On duty');
    expect(rows[0].querySelectorAll('td')).toHaveLength(12);
    expect(rows[0].querySelector('[data-testid="night-duty-cell"]')?.textContent?.trim()).toBe('');
    expect(rows[0].textContent).not.toContain('Mom');
    const cells = rows[1].querySelectorAll('td');
    expect(cells[1].getAttribute('data-testid')).toBe('night-duty-cell');
    expect(cells[1].textContent?.replace(/\s+/g, ' ').trim()).toBe('👤 Dad Screen night');
    expect(cells[1].querySelector('[aria-hidden="true"]')?.textContent).toBe('👤');
  });

  it('keeps the header and each night together when printed', async () => {
    const { host, rows } = await setup();

    expect(host.querySelector('thead')?.classList).toContain('print:table-header-group');
    expect(rows.every((row) => row.classList.contains('break-inside-avoid'))).toBe(true);
    expect(host.firstElementChild?.classList).toContain('relative');
  });

  it('shows a placeholder when there are no hygiene notes', async () => {
    await TestBed.configureTestingModule({ imports: [SleepDiarySheet] }).compileComponents();
    const fixture = TestBed.createComponent(SleepDiarySheet);
    fixture.componentRef.setInput('days', []);
    fixture.componentRef.setInput('entries', []);
    fixture.componentRef.setInput('hygieneNotes', '');
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('None recorded.');
  });
});
