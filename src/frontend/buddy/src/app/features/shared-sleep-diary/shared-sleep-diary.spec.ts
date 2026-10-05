import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  SharedSleepDiary as SharedSleepDiaryData,
  SleepDiaryService,
} from '../../core/sleep-diary.service';
import { SharedSleepDiary } from './shared-sleep-diary';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const shared: SharedSleepDiaryData = {
  childGivenName: 'Alex',
  childFamilyName: 'Anderson',
  from: '2026-03-02',
  to: '2026-03-15',
  expiresAt: '2026-04-01T00:00:00Z',
  sleepHygieneNotes: 'Blackout curtains',
  entries: [
    {
      date: '2026-03-07',
      isWeekend: true,
      loggedBy: 'g',
      routineStartTime: '19:00',
      ritualStartTime: '19:30',
      ritualEndTime: '20:10',
      bedTime: '20:15',
      fellAsleepTime: '20:45',
      nightWakeUps: [{ startTime: '03:30', durationMinutes: 30 }],
      morningWakeTime: '06:30',
      isTired: true,
      naps: [{ startTime: '13:00', durationMinutes: 45 }],
      totalSleepMinutes: 555,
      remarks: 'Cried before settling',
    },
  ],
};

describe('SharedSleepDiary', () => {
  async function setup(getShared: SleepDiaryService['getShared']) {
    const service: Partial<SleepDiaryService> = { getShared: vi.fn(getShared) };

    await TestBed.configureTestingModule({
      imports: [SharedSleepDiary],
      providers: [
        { provide: SleepDiaryService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ token: 'secret' }) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(SharedSleepDiary);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, service };
  }

  it('renders the paper-form table for every day in the range', async () => {
    const { compiled, service } = await setup(async () => shared);

    expect(service.getShared).toHaveBeenCalledWith('secret', undefined, undefined);
    expect(compiled.querySelector('h1')?.textContent).toContain('Sleep diary for Alex Anderson');
    expect(compiled.querySelectorAll('thead th')).toHaveLength(12);

    const rows = compiled.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(14);
    const logged = rows[5].textContent ?? '';
    expect(logged).toContain('Sat');
    expect(logged).toContain('7:30');
    expect(logged).toContain('3:30');
    expect(logged).toContain('30 min');
    expect(logged).toContain('9 h 15 min');
    expect(logged).toContain('Cried before settling');
    expect(compiled.textContent).toContain('Blackout curtains');
    expect(compiled.textContent).toContain('This link expires Apr 1, 2026.');
  });

  it('pages to the previous 14 days', async () => {
    const { fixture, compiled, service } = await setup(async () => shared);

    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Previous 14 days')!
      .click();
    await settle(fixture);

    expect(service.getShared).toHaveBeenLastCalledWith('secret', '2026-02-16', '2026-03-01');
  });

  it('says so when the link is invalid, revoked or expired', async () => {
    const { compiled } = await setup(async () =>
      Promise.reject(new HttpErrorResponse({ status: 404 })),
    );

    expect(compiled.textContent).toContain(
      'This link is invalid, has been revoked or has expired.',
    );
  });

  it('shows a generic error for other failures', async () => {
    const { compiled } = await setup(async () =>
      Promise.reject(new HttpErrorResponse({ status: 500 })),
    );

    expect(compiled.textContent).toContain('Unable to load the sleep diary.');
  });
});
