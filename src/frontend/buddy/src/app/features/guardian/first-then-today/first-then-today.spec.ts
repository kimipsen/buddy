import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  FirstThenBoard,
  FirstThenService,
  GuardianFirstThen,
} from '../../../core/first-then.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { FirstThenToday } from './first-then-today';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return { id, name: { givenName, familyName: 'Hansen' }, timeZoneId: 'UTC' } as ChildSummary;
}

function board(overrides: Partial<FirstThenBoard> = {}): FirstThenBoard {
  return {
    revision: 1,
    state: 'Waiting',
    first: { title: 'Homework', icon: '📚' },
    then: { title: 'iPad', icon: '📱' },
    thenMinutes: null,
    unlockedAt: null,
    // Now, so the board is current whenever the spec runs.
    lastModifiedAt: new Date().toISOString(),
    lastModifiedByName: null,
    ...overrides,
  };
}

const none = board({ state: 'None', first: null, then: null });

const view = (b: FirstThenBoard): GuardianFirstThen => ({
  board: b,
  recentFirsts: [],
  recentThens: [],
});

describe('FirstThenToday', () => {
  async function setup(
    boards: Record<string, FirstThenBoard>,
    getBoard?: (childId: string) => Promise<GuardianFirstThen>,
  ) {
    const firstThen: Partial<FirstThenService> = {
      getBoard: vi.fn(getBoard ?? (async (childId: string) => view(boards[childId] ?? none))),
    };
    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child('emil', 'Emil'), child('ida', 'Ida')]),
    };

    await TestBed.configureTestingModule({
      imports: [FirstThenToday],
      providers: [
        provideRouter([]),
        { provide: FirstThenService, useValue: firstThen },
        { provide: GuardiansService, useValue: guardians },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FirstThenToday);
    await settle(fixture);

    return { compiled: fixture.nativeElement as HTMLElement, firstThen };
  }

  it('lists only the children with a current board, with its state', async () => {
    const { compiled, firstThen } = await setup({
      emil: board(),
      ida: board({ state: 'Unlocked', then: { title: 'Lego', icon: '🧱' } }),
    });

    expect(firstThen.getBoard).toHaveBeenCalledWith('emil');
    expect(firstThen.getBoard).toHaveBeenCalledWith('ida');
    const rows = [...compiled.querySelectorAll('li')].map((li) => li.textContent!);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain('Emil');
    expect(rows[0]).toContain('First Homework, then iPad');
    expect(rows[1]).toContain('Ida');
    expect(rows[1]).toContain('Now: Lego');
    expect(compiled.querySelector('a[href="/guardian/first-then"]')?.textContent?.trim()).toBe(
      'Open',
    );
  });

  it('leaves out a child without a board', async () => {
    const { compiled } = await setup({ emil: board() });

    const rows = [...compiled.querySelectorAll('li')].map((li) => li.textContent!);
    expect(rows).toHaveLength(1);
    expect(rows[0]).toContain('Emil');
  });

  it('leaves out a board left over from yesterday', async () => {
    const { compiled } = await setup({
      emil: board(),
      ida: board({ lastModifiedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString() }),
    });

    const rows = [...compiled.querySelectorAll('li')].map((li) => li.textContent!);
    expect(rows).toHaveLength(1);
    expect(rows[0]).toContain('Emil');
  });

  it('renders nothing when no child has a board', async () => {
    const { compiled } = await setup({});

    expect(compiled.querySelector('[data-testid="first-then-today"]')).toBeNull();
  });

  it('renders nothing when the boards cannot be loaded', async () => {
    const { compiled } = await setup({}, async () => {
      throw new Error('offline');
    });

    expect(compiled.querySelector('[data-testid="first-then-today"]')).toBeNull();
  });
});
