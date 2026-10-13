import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import type { FeelingCheckIn, FeelingZone } from '../../../../core/feelings.service';
import { FeelingsWeeks } from './feelings-weeks';

function checkIn(id: string, date: string, zone: FeelingZone): FeelingCheckIn {
  return {
    id,
    zone,
    tags: [],
    note: '',
    date,
    time: '12:00',
    loggedBy: 'child-1',
    isSelfReport: true,
    recordedAt: `${date}T10:00:00Z`,
  };
}

describe('FeelingsWeeks', () => {
  it('counts each zone per week for the eight weeks ending on the last day', async () => {
    await TestBed.configureTestingModule({ imports: [FeelingsWeeks] }).compileComponents();
    const fixture = TestBed.createComponent(FeelingsWeeks);
    fixture.componentRef.setInput('end', '2026-10-13');
    fixture.componentRef.setInput('checkIns', [
      checkIn('a', '2026-10-13', 'Green'),
      checkIn('b', '2026-10-12', 'Green'),
      checkIn('c', '2026-10-07', 'Red'),
      checkIn('d', '2026-10-06', 'Yellow'),
    ]);
    fixture.detectChanges();

    const weeks = (fixture.nativeElement as HTMLElement).querySelectorAll('li');
    expect(weeks).toHaveLength(8);

    // The last week runs 2026-10-07 .. 2026-10-13.
    const last = weeks[7].textContent ?? '';
    expect(last).toContain('3 check-ins');
    expect(last).toContain('Ready (green): 2');
    expect(last).toContain('Too much (red): 1');
    expect(last).not.toContain('Wound up');
    expect(weeks[6].textContent).toContain('Wound up (yellow): 1');
    expect(weeks[0].textContent).toContain('No check-ins');
  });
});
