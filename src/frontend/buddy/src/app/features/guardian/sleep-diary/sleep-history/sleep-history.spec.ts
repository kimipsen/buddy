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

    const host = fixture.nativeElement as HTMLElement;
    const rows = Array.from(host.querySelectorAll('tbody tr'));
    const nights = Array.from(host.querySelectorAll<HTMLElement>('ul > li'));
    return { host, rows, nights, selectDate };
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

  // jsdom has no Tailwind, so both layouts render; the browser shows one by width.
  it('shows the table from md up and the night list below md, and prints the table', async () => {
    const { host } = await setup();

    const tableBox = host.querySelector('table')!.parentElement!;
    const list = host.querySelector('ul')!;
    expect(tableBox.classList).toContain('max-md:not-print:hidden');
    expect(tableBox.classList).toContain('relative');
    expect(list.classList).toContain('md:hidden');
    expect(list.classList).toContain('print:hidden');
    expect(list.classList).toContain('relative');
  });

  it('lists the same nights below md, newest first, as headed blocks', async () => {
    const { nights } = await setup();

    expect(nights).toHaveLength(2);
    expect(nights[0].querySelector('h4')?.textContent).toContain('Mar 3');
    expect(nights[1].querySelector('h4')?.textContent).toContain('Mar 2');
  });

  it('labels each logged value in the night list', async () => {
    const { nights } = await setup();

    const values = Object.fromEntries(
      Array.from(nights[0].querySelectorAll('dl > div')).map((pair) => [
        pair.querySelector('dt')?.textContent?.trim(),
        pair.querySelector('dd')?.textContent?.trim(),
      ]),
    );
    expect(values).toEqual({
      'Lies down': '8:15 PM',
      'Falls asleep': '8:45 PM',
      'Wakes up': '6:30 AM',
      Slept: '9 h 15 min',
      'Wake-ups': '2',
      Naps: '0',
    });
    expect(nights[0].textContent).toContain('Tired');
    expect(nights[0].textContent).not.toContain('Not logged');
  });

  it('shows a night not logged as one line without values', async () => {
    const { nights } = await setup();

    expect(nights[1].textContent).toContain('Not logged');
    expect(nights[1].querySelector('dl')).toBeNull();
    expect(nights[1].textContent).not.toContain('Tired');
  });

  it('highlights the selected night in the list', async () => {
    const { nights } = await setup();

    expect(nights[0].getAttribute('aria-current')).toBe('date');
    expect(nights[0].classList).toContain('bg-emerald-50');
    expect(nights[1].getAttribute('aria-current')).toBeNull();
    expect(nights[1].classList).not.toContain('bg-emerald-50');
  });

  it('emits the night chosen for editing from the list', async () => {
    const { nights, selectDate } = await setup();

    const edit = nights[1].querySelector('button') as HTMLButtonElement;
    expect(edit.textContent?.trim()).toBe('Edit');
    expect(edit.getAttribute('aria-label')).toBe('Edit Mon, Mar 2');
    edit.click();

    expect(selectDate).toHaveBeenCalledOnce();
    expect(selectDate).toHaveBeenCalledWith('2026-03-02');
  });
});
