import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ChildClockService } from '../../../../core/child-clock.service';
import { FirstThenBoard, FirstThenService } from '../../../../core/first-then.service';
import { FIRST_THEN_POLL_MS } from '../../first-then/child-first-then';
import { FirstThenCard } from './first-then-card';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const TODAY = '2026-10-14';

function board(overrides: Partial<FirstThenBoard> = {}): FirstThenBoard {
  return {
    revision: 1,
    state: 'Waiting',
    first: { title: 'Homework', icon: '📚' },
    then: { title: 'iPad', icon: '📱' },
    thenMinutes: 20,
    unlockedAt: null,
    lastModifiedAt: `${TODAY}T10:00:00Z`,
    lastModifiedByName: null,
    ...overrides,
  };
}

describe('FirstThenCard', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function setup(getMyBoard: () => Promise<FirstThenBoard>) {
    const firstThen: Partial<FirstThenService> = { getMyBoard: vi.fn(getMyBoard) };

    await TestBed.configureTestingModule({
      imports: [FirstThenCard],
      providers: [
        provideRouter([]),
        { provide: FirstThenService, useValue: firstThen },
        {
          provide: ChildClockService,
          useValue: {
            now: () => Date.parse(`${TODAY}T12:00:00Z`),
            today: () => TODAY,
            timeZone: signal('UTC').asReadonly(),
          } as unknown as ChildClockService,
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FirstThenCard);
    await settle(fixture);

    return {
      fixture,
      compiled: fixture.nativeElement as HTMLElement,
      firstThen,
      card: () => fixture.nativeElement.querySelector('[data-testid="first-then-card"]'),
    };
  }

  it('links a waiting board to the board page', async () => {
    const { card } = await setup(async () => board());

    expect(card()!.getAttribute('href')).toBe('/child/first-then');
    expect(card()!.textContent).toContain('First Homework, then iPad');
  });

  it('says the Then is on once unlocked', async () => {
    const { card } = await setup(async () =>
      board({ state: 'Unlocked', unlockedAt: `${TODAY}T11:00:00Z` }),
    );

    expect(card()!.textContent).toContain('Now: iPad!');
  });

  it('renders nothing without a current board', async () => {
    const { card } = await setup(async () => board({ state: 'None', first: null, then: null }));

    expect(card()).toBeNull();
  });

  it('hides a board left over from yesterday', async () => {
    const { card } = await setup(async () => board({ lastModifiedAt: '2026-10-13T18:00:00Z' }));

    expect(card()).toBeNull();
  });

  it('renders nothing when the board cannot be loaded', async () => {
    const { card } = await setup(async () => {
      throw new Error('offline');
    });

    expect(card()).toBeNull();
  });

  it('keeps the last board on screen when a poll fails', async () => {
    let fail = false;
    const { fixture, card } = await setup(async () => {
      if (fail) {
        throw new Error('offline');
      }
      return board();
    });

    fail = true;
    document.dispatchEvent(new Event('visibilitychange'));
    await settle(fixture);

    expect(card()).not.toBeNull();
  });

  it('polls while open and stops when destroyed', async () => {
    const intervals = vi.spyOn(globalThis, 'setInterval');
    const cleared = vi.spyOn(globalThis, 'clearInterval');
    const { fixture, firstThen } = await setup(async () => board());

    expect(intervals).toHaveBeenCalledWith(expect.any(Function), FIRST_THEN_POLL_MS);
    document.dispatchEvent(new Event('visibilitychange'));
    await settle(fixture);
    expect(firstThen.getMyBoard).toHaveBeenCalledTimes(2);

    fixture.destroy();
    expect(cleared).toHaveBeenCalled();
  });
});
