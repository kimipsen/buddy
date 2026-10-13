import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FeelingsService } from '../../../core/feelings.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { FeelingsToday } from './feelings-today';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

describe('FeelingsToday', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(children: ChildSummary[], listCheckIns: FeelingsService['listCheckIns']) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-10-13T12:00:00'));

    await TestBed.configureTestingModule({
      imports: [FeelingsToday],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: { listMyChildren: vi.fn(async () => children) } },
        { provide: FeelingsService, useValue: { listCheckIns } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FeelingsToday);
    await settle(fixture);
    return fixture.nativeElement as HTMLElement;
  }

  const emil = { id: 'emil', name: { givenName: 'Emil' } } as ChildSummary;
  const ida = { id: 'ida', name: { givenName: 'Ida' } } as ChildSummary;

  it('shows each child’s check-ins today as face chips, and a link to the page', async () => {
    const listCheckIns = vi.fn(async (childId: string) => ({
      from: '2026-10-13',
      to: '2026-10-13',
      promptTimes: [],
      displayMode: 'FacesAndWords' as const,
      checkIns:
        childId === 'emil'
          ? [
              {
                id: 'c1',
                zone: 'Green' as const,
                tags: [],
                note: '',
                date: '2026-10-13',
                time: '07:40',
                loggedBy: 'emil',
                isSelfReport: true,
                recordedAt: '2026-10-13T05:40:00Z',
              },
            ]
          : [],
    }));
    const el = await setup([emil, ida], listCheckIns);

    expect(listCheckIns).toHaveBeenCalledWith('emil', '2026-10-13', '2026-10-13');
    const rows = el.querySelectorAll('[data-testid="feelings-today"] > li');
    expect(rows[0].textContent).toContain('07:40');
    expect(rows[0].textContent).toContain('Ready');
    expect(rows[1].textContent).toContain('No check-ins yet');
    expect(el.querySelector('a[href="/guardian/feelings"]')).toBeTruthy();
  });

  it('shows the load error, and the no-children state', async () => {
    const failing = await setup(
      [emil],
      vi.fn(async () => Promise.reject(new Error('down'))),
    );
    expect(failing.textContent).toContain('Unable to load today’s check-ins.');

    TestBed.resetTestingModule();
    const empty = await setup([], vi.fn());
    expect(empty.textContent).toContain('No children linked yet.');
  });
});
