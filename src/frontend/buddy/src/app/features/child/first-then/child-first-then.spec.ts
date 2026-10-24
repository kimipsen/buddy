import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Mock, afterEach, describe, expect, it, vi } from 'vitest';

import { ChildClockService } from '../../../core/child-clock.service';
import { FirstThenBoard, FirstThenService } from '../../../core/first-then.service';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildFirstThen, FIRST_THEN_POLL_MS, THEN_TICK_MS } from './child-first-then';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const TODAY = '2026-10-14';
const NOW = Date.parse('2026-10-14T15:05:00Z');
const UNLOCKED_AT = '2026-10-14T15:00:00+00:00';

function board(overrides: Partial<FirstThenBoard> = {}): FirstThenBoard {
  return {
    revision: 1,
    state: 'Waiting',
    first: { title: 'Homework', icon: '📚' },
    then: { title: 'iPad', icon: '📱' },
    thenMinutes: 20,
    unlockedAt: null,
    lastModifiedAt: '2026-10-14T14:00:00+00:00',
    lastModifiedByName: null,
    ...overrides,
  };
}

const unlocked = (overrides: Partial<FirstThenBoard> = {}) =>
  board({ state: 'Unlocked', unlockedAt: UNLOCKED_AT, ...overrides });

const none: FirstThenBoard = {
  ...board(),
  state: 'None',
  first: null,
  then: null,
  thenMinutes: null,
};

interface Options {
  myBoard?: FirstThenBoard;
  firstThen?: Partial<FirstThenService>;
  showVisualTimers?: boolean;
}

describe('ChildFirstThen', () => {
  afterEach(() => {
    // Mocks before the real timers, as in child-focus.spec.ts.
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  async function setup(options: Options = {}) {
    // The shared timer disc reads Date.now() itself, so the real clock must agree with the stub.
    // The intervals are faked too, so a test can step the poll.
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(NOW);

    const stub = {
      getMyBoard: vi.fn(async () => options.myBoard ?? board()),
      actOnMyBoard: vi.fn(async () => unlocked()),
      ...options.firstThen,
    } as {
      getMyBoard: Mock<() => Promise<FirstThenBoard>>;
      actOnMyBoard: Mock<(...args: unknown[]) => Promise<FirstThenBoard>>;
    };
    const users: Partial<UsersService> = {
      ensureCurrentUser: vi.fn(
        async () => ({ showVisualTimers: options.showVisualTimers ?? true }) as CurrentUser,
      ),
      timeZoneId: signal('UTC').asReadonly(),
      profileTimeZoneId: signal<string | null>('UTC').asReadonly(),
    };

    await TestBed.configureTestingModule({
      imports: [ChildFirstThen],
      providers: [
        provideRouter([]),
        { provide: FirstThenService, useValue: stub },
        { provide: UsersService, useValue: users },
        {
          provide: ChildClockService,
          useValue: {
            // Follows the faked Date, so a test can let time pass.
            now: () => Date.now(),
            today: () => TODAY,
            timeZone: signal('UTC').asReadonly(),
          } as unknown as ChildClockService,
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildFirstThen);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, firstThen: stub };
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return [...compiled.querySelectorAll('button')].find((b) => b.textContent?.trim() === text);
  }

  function liveRegion(compiled: HTMLElement): string {
    return compiled.querySelector('[aria-live="polite"]')?.textContent?.trim() ?? '';
  }

  it('shows pictogram cards as their pictures, next to the titles', async () => {
    const { compiled } = await setup({
      myBoard: board({
        first: { title: 'Homework', icon: 'pictogram:homework' },
        then: { title: 'iPad', icon: 'pictogram:screen-time' },
      }),
    });

    const first = compiled.querySelector('[data-testid="first-then-first"]')!;
    expect(first.querySelector('img')!.getAttribute('src')).toBe('/pictograms/homework.svg');
    expect(first.textContent).not.toContain('pictogram:');
    expect(compiled.querySelector('img[src="/pictograms/screen-time.svg"]')).not.toBeNull();
  });

  it('shows the spinner until the board arrives', async () => {
    const { compiled } = await setup({
      firstThen: { getMyBoard: vi.fn(() => new Promise<FirstThenBoard>(() => undefined)) },
    });

    expect(compiled.querySelector('app-loading-spinner')).not.toBeNull();
    expect(compiled.querySelector('[data-testid="first-then-first"]')).toBeNull();
  });

  it('shows a waiting board: the First with Done, the Then locked after it', async () => {
    const { compiled } = await setup();

    const first = compiled.querySelector('[data-testid="first-then-first"]')!;
    const then = compiled.querySelector('[data-testid="first-then-then"]')!;
    expect(first.textContent).toContain('First');
    expect(first.textContent).toContain('Homework');
    expect(button(compiled, 'Done!')).toBeDefined();
    expect(then.textContent).toContain('Then');
    expect(then.textContent).toContain('iPad');
    expect(then.textContent).toContain('After Homework');
    expect(button(compiled, 'All done')).toBeUndefined();
    expect(compiled.querySelector('[data-testid="first-then-changed"]')).toBeNull();
    expect(liveRegion(compiled)).toBe('');
  });

  it('does not play the unlock animation for a board that loads already unlocked', async () => {
    const { compiled } = await setup({ myBoard: unlocked() });

    expect(compiled.querySelector('[data-testid="first-then-then"]')!.classList).not.toContain(
      'motion-safe:animate-scale-in',
    );
  });

  it('ticks the First with the revision it shows, unlocks the Then and announces it', async () => {
    const { fixture, compiled, firstThen } = await setup({ myBoard: board({ revision: 4 }) });

    button(compiled, 'Done!')!.click();
    await settle(fixture);

    expect(firstThen.actOnMyBoard).toHaveBeenCalledWith('first/complete', 4);
    expect(compiled.querySelector('[data-testid="first-then-then"]')!.textContent).toContain(
      'Now: iPad!',
    );
    expect(button(compiled, 'Not done yet')).toBeDefined();
    expect(button(compiled, 'All done')).toBeDefined();
    expect(liveRegion(compiled)).toBe('Now: iPad!');
    expect(compiled.querySelector('[data-testid="first-then-then"]')!.classList).toContain(
      'motion-safe:animate-scale-in',
    );
    expect(compiled.querySelector('[data-testid="first-then-changed"]')).toBeNull();
  });

  it('sends one tick for a double tap, with the button disabled while it is out', async () => {
    const { fixture, compiled, firstThen } = await setup();
    firstThen.actOnMyBoard.mockImplementation(() => new Promise<FirstThenBoard>(() => undefined));

    button(compiled, 'Done!')!.click();
    await settle(fixture);
    button(compiled, 'Done!')!.click();
    await settle(fixture);

    expect(firstThen.actOnMyBoard).toHaveBeenCalledTimes(1);
    expect(button(compiled, 'Done!')!.disabled).toBe(true);
  });

  it('undoes a mis-tap and ends the board', async () => {
    const { fixture, compiled, firstThen } = await setup({
      myBoard: unlocked(),
      firstThen: {
        actOnMyBoard: vi.fn().mockResolvedValueOnce(board()) as FirstThenService['actOnMyBoard'],
      },
    });

    button(compiled, 'Not done yet')!.click();
    await settle(fixture);
    expect(firstThen.actOnMyBoard).toHaveBeenLastCalledWith('first/reopen', 1);
    expect(button(compiled, 'Done!')).toBeDefined();
    expect(compiled.querySelector('[data-testid="first-then-then"]')!.classList).not.toContain(
      'motion-safe:animate-scale-in',
    );

    // Back to unlocked, then All done.
    (firstThen.actOnMyBoard as ReturnType<typeof vi.fn>).mockResolvedValueOnce(unlocked());
    button(compiled, 'Done!')!.click();
    await settle(fixture);
    (firstThen.actOnMyBoard as ReturnType<typeof vi.fn>).mockResolvedValueOnce(none);
    button(compiled, 'All done')!.click();
    await settle(fixture);

    expect(firstThen.actOnMyBoard).toHaveBeenLastCalledWith('end', 1);
    expect(compiled.querySelector('[data-testid="first-then-none"]')!.textContent).toContain(
      'No board right now',
    );
  });

  it('shows the countdown disc for an unlocked Then with a length', async () => {
    const { compiled } = await setup({ myBoard: unlocked() });

    const timer = compiled.querySelector('[role="timer"]');
    expect(timer).not.toBeNull();
    expect(timer!.getAttribute('aria-label')).toBe('15 min left');
  });

  it('shows no disc when visual timers are off, but still says time’s up when it has passed', async () => {
    const running = await setup({ myBoard: unlocked(), showVisualTimers: false });
    expect(running.compiled.querySelector('[role="timer"]')).toBeNull();
    expect(running.compiled.textContent).not.toContain('Time’s up');
    TestBed.resetTestingModule();

    const over = await setup({
      myBoard: unlocked({ unlockedAt: '2026-10-14T14:00:00+00:00' }),
      showVisualTimers: false,
    });
    expect(over.compiled.querySelector('[role="timer"]')).toBeNull();
    expect(over.compiled.textContent).toContain("Time's up");
  });

  it('says time’s up the moment the Then runs out, without waiting for a poll', async () => {
    // 20 minutes from 14:45:10 runs out at 15:05:10, ten seconds after NOW.
    const { fixture, compiled } = await setup({
      myBoard: unlocked({ unlockedAt: '2026-10-14T14:45:10+00:00' }),
      showVisualTimers: false,
    });
    expect(compiled.textContent).not.toContain("Time's up");

    vi.advanceTimersByTime(THEN_TICK_MS);
    await settle(fixture);

    expect(compiled.textContent).toContain("Time's up");
  });

  it('ticks only while an unlocked Then is counting down', async () => {
    const counting = await setup({ myBoard: unlocked(), showVisualTimers: false });
    const ticking = vi.getTimerCount();
    counting.firstThen.actOnMyBoard.mockResolvedValueOnce(none);
    button(counting.compiled, 'All done')!.click();
    await settle(counting.fixture);
    // Only the poll is left once the board has ended.
    expect(vi.getTimerCount()).toBe(ticking - 1);
    TestBed.resetTestingModule();

    await setup({ myBoard: board(), showVisualTimers: false });
    expect(vi.getTimerCount()).toBe(ticking - 1);
    TestBed.resetTestingModule();

    await setup({ myBoard: unlocked({ thenMinutes: null }), showVisualTimers: false });
    expect(vi.getTimerCount()).toBe(ticking - 1);
  });

  it('reads the time afresh when a poll brings a Then that has already run out', async () => {
    const { fixture, compiled, firstThen } = await setup({ showVisualTimers: false });
    // Runs out at 15:05:30, between the first load and the poll a minute later.
    firstThen.getMyBoard.mockResolvedValue(unlocked({ unlockedAt: '2026-10-14T14:45:30+00:00' }));

    vi.advanceTimersByTime(FIRST_THEN_POLL_MS);
    await settle(fixture);

    expect(compiled.textContent).toContain("Time's up");
  });

  it('reads the time afresh after a tick', async () => {
    const { fixture, compiled, firstThen } = await setup({ showVisualTimers: false });
    firstThen.actOnMyBoard.mockResolvedValueOnce(
      unlocked({ unlockedAt: '2026-10-14T15:00:00+00:00' }),
    );
    // The page sat open for 25 minutes; the 20-minute Then the server unlocked at 15:00 is over.
    vi.setSystemTime(NOW + 25 * 60_000);

    button(compiled, 'Done!')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain("Time's up");
  });

  it('shows no disc for a Then without a length', async () => {
    const { compiled } = await setup({ myBoard: unlocked({ thenMinutes: null }) });

    expect(compiled.querySelector('[role="timer"]')).toBeNull();
    expect(compiled.textContent).not.toContain("Time's up");
  });

  it('reads no board, or a board from yesterday, as nothing right now', async () => {
    const empty = await setup({ myBoard: none });
    expect(empty.compiled.textContent).toContain('No board right now');
    expect(empty.compiled.querySelector('a[href="/child"]')).not.toBeNull();
    TestBed.resetTestingModule();

    const yesterday = await setup({
      myBoard: board({ lastModifiedAt: '2026-10-13T18:00:00+00:00' }),
    });
    expect(yesterday.compiled.textContent).toContain('No board right now');
  });

  it('reloads and shows the new board when a guardian replaced it since it loaded', async () => {
    const replaced = board({ revision: 2, first: { title: 'Tidy your room', icon: '🧹' } });
    const { fixture, compiled, firstThen } = await setup({
      firstThen: {
        actOnMyBoard: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 409, error: { code: 'first_then_board_changed' } });
        }),
      },
    });
    firstThen.getMyBoard.mockResolvedValue(replaced);

    button(compiled, 'Done!')!.click();
    await settle(fixture);

    expect(firstThen.getMyBoard).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('[data-testid="first-then-first"]')!.textContent).toContain(
      'Tidy your room',
    );
    expect(compiled.querySelector('[data-testid="first-then-changed"]')!.textContent).toContain(
      'Your board has changed.',
    );
    expect(liveRegion(compiled)).toBe('Your board has changed.');
  });

  it('keeps the board on screen and says so when a tick fails for another reason', async () => {
    const { fixture, compiled } = await setup({
      firstThen: {
        actOnMyBoard: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 0 });
        }),
      },
    });

    button(compiled, 'Done!')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('That didn’t work. Try again.');
    expect(button(compiled, 'Done!')).toBeDefined();
  });

  it('clears the error when the next try works', async () => {
    const { fixture, compiled, firstThen } = await setup();
    firstThen.actOnMyBoard.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));

    button(compiled, 'Done!')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('That didn’t work. Try again.');

    button(compiled, 'Done!')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('That didn’t work. Try again.');
  });

  it('keeps the board on screen when a later poll fails', async () => {
    const { fixture, compiled, firstThen } = await setup();
    firstThen.getMyBoard.mockRejectedValue(new HttpErrorResponse({ status: 0 }));

    vi.advanceTimersByTime(FIRST_THEN_POLL_MS);
    await settle(fixture);

    expect(firstThen.getMyBoard).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).not.toContain('Could not load your board.');
    expect(compiled.querySelector('[data-testid="first-then-first"]')!.textContent).toContain(
      'Homework',
    );
  });

  it('shows a load error only when there is no board to keep showing', async () => {
    const { compiled } = await setup({
      firstThen: {
        getMyBoard: vi.fn(async () => {
          throw new HttpErrorResponse({ status: 500 });
        }),
      },
    });

    expect(compiled.textContent).toContain('Could not load your board.');
  });

  it('announces the unlock again after an undo', async () => {
    const { fixture, compiled, firstThen } = await setup();

    button(compiled, 'Done!')!.click();
    await settle(fixture);
    expect(liveRegion(compiled)).toBe('Now: iPad!');

    firstThen.actOnMyBoard.mockResolvedValueOnce(board());
    button(compiled, 'Not done yet')!.click();
    await settle(fixture);
    expect(liveRegion(compiled)).toBe('');
    expect(compiled.querySelector('[data-testid="first-then-then"]')!.classList).not.toContain(
      'motion-safe:animate-scale-in',
    );

    button(compiled, 'Done!')!.click();
    await settle(fixture);
    expect(liveRegion(compiled)).toBe('Now: iPad!');
  });

  it('ignores a poll answer that a tick overtook', async () => {
    const { fixture, compiled, firstThen } = await setup();
    let answerPoll: (board: FirstThenBoard) => void = () => undefined;
    firstThen.getMyBoard.mockImplementationOnce(
      () => new Promise<FirstThenBoard>((resolve) => (answerPoll = resolve)),
    );

    document.dispatchEvent(new Event('visibilitychange'));
    button(compiled, 'Done!')!.click();
    await settle(fixture);
    answerPoll(board());
    await settle(fixture);

    expect(compiled.querySelector('[data-testid="first-then-then"]')!.textContent).toContain(
      'Now: iPad!',
    );
  });

  it('polls for a guardian’s new board and stops when destroyed', async () => {
    const { fixture, compiled, firstThen } = await setup();
    firstThen.getMyBoard.mockResolvedValue(
      board({ revision: 2, then: { title: 'TV', icon: '📺' } }),
    );

    vi.advanceTimersByTime(FIRST_THEN_POLL_MS);
    await settle(fixture);

    expect(firstThen.getMyBoard).toHaveBeenCalledTimes(2);
    expect(compiled.querySelector('[data-testid="first-then-then"]')!.textContent).toContain('TV');

    fixture.destroy();
    vi.advanceTimersByTime(FIRST_THEN_POLL_MS * 3);
    expect(firstThen.getMyBoard).toHaveBeenCalledTimes(2);
  });
});
