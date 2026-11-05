import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../core/date-utils';
import { readOwnTimer, writeOwnTimer } from '../../../core/own-timer-storage';
import { ScreenWakeLockService } from '../../../core/screen-wake-lock.service';
import { TimeUpChimeService } from '../../../core/time-up-chime.service';
import { DEFAULT_TIMER_PREFERENCES, TimerPreferences } from '../../../core/timer-preferences';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildTimerPage } from './child-timer-page';

const MINUTE = 60_000;

describe('ChildTimerPage', () => {
  const today = todayIsoDate();
  const NOW = Date.parse(`${today}T12:00:00Z`);

  let me: WritableSignal<CurrentUser | null>;
  let updateTimerPreferences: ReturnType<typeof vi.fn>;

  const child = (preferences: Partial<TimerPreferences> = {}, allowChime = true): CurrentUser =>
    ({
      showVisualTimers: true,
      allowChime,
      timerPreferences: { ...DEFAULT_TIMER_PREFERENCES, ...preferences },
    }) as CurrentUser;

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers({
      toFake: ['Date', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval'],
    });
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    localStorage.clear();
  });

  async function setup(user: CurrentUser = child()): Promise<ComponentFixture<ChildTimerPage>> {
    me = signal<CurrentUser | null>(user);
    updateTimerPreferences = vi.fn(async (timerPreferences: TimerPreferences) => {
      const updated = { ...me()!, timerPreferences };
      me.set(updated);
      return updated;
    });

    await TestBed.configureTestingModule({
      imports: [ChildTimerPage],
      providers: [
        provideRouter([]),
        {
          provide: UsersService,
          useValue: {
            currentUser: me.asReadonly(),
            timeZoneId: signal('UTC').asReadonly(),
            profileTimeZoneId: signal<string | null>('UTC').asReadonly(),
            updateTimerPreferences,
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildTimerPage);
    await settle(fixture);
    return fixture;
  }

  async function settle(fixture: { detectChanges: () => void }) {
    for (let round = 0; round < 3; round++) {
      fixture.detectChanges();
      await vi.advanceTimersByTimeAsync(0);
    }
    fixture.detectChanges();
  }

  const page = (fixture: ComponentFixture<ChildTimerPage>) => fixture.nativeElement as HTMLElement;

  function button(fixture: ComponentFixture<ChildTimerPage>, text: string) {
    return Array.from(page(fixture).querySelectorAll('button')).find(
      (element) =>
        element.textContent?.trim().includes(text) || element.getAttribute('aria-label') === text,
    );
  }

  function colour(fixture: ComponentFixture<ChildTimerPage>, name: string): HTMLInputElement {
    const label = Array.from(page(fixture).querySelectorAll('fieldset label')).find(
      (candidate) => candidate.textContent?.trim() === name,
    );
    expect(label, `colour "${name}"`).toBeDefined();
    return label!.querySelector('input[type="radio"]')!;
  }

  async function pickColour(fixture: ComponentFixture<ChildTimerPage>, name: string) {
    colour(fixture, name).click();
    await settle(fixture);
  }

  function ownTimerLabel(fixture: ComponentFixture<ChildTimerPage>) {
    return page(fixture)
      .querySelector('[data-testid="own-timer"] [role="timer"]')
      ?.getAttribute('aria-label');
  }

  async function click(fixture: ComponentFixture<ChildTimerPage>, text: string) {
    const target = button(fixture, text);
    expect(target, `button "${text}"`).toBeDefined();
    target!.click();
    await settle(fixture);
  }

  function radioLabels(fixture: ComponentFixture<ChildTimerPage>, group: string): string[] {
    return Array.from(
      page(fixture).querySelectorAll(`[role="radiogroup"][aria-label="${group}"] [role="radio"]`),
    ).map((radio) => radio.textContent!.trim());
  }

  it('offers every shape, size, colour and sound by name', async () => {
    const fixture = await setup();

    expect(radioLabels(fixture, 'Shape')).toEqual(['Circle', 'Bar']);
    expect(radioLabels(fixture, 'Size')).toEqual(['Small', 'Medium', 'Large']);
    expect(radioLabels(fixture, 'Sound')).toEqual(['Bell', 'Xylophone', 'Gong']);
    expect(page(fixture).querySelector('fieldset legend')?.textContent?.trim()).toBe('Colour');
    expect(
      Array.from(page(fixture).querySelectorAll('fieldset label')).map((label) =>
        label.textContent?.trim(),
      ),
    ).toEqual(['Same as the routine', 'Green', 'Blue', 'Purple', 'Pink', 'Orange', 'Yellow']);
    expect(
      Array.from(page(fixture).querySelectorAll<HTMLInputElement>('fieldset input')).map(
        (input) => input.name,
      ),
    ).toEqual(Array(7).fill('timer-color'));
  });

  it('offers four preset lengths and a preview of the timer before one starts', async () => {
    const fixture = await setup();
    // Most of the preview's 5 minutes are left: a wedge, not a full disc.
    expect(page(fixture).querySelector('[data-testid="timer-preview"] svg path')).not.toBeNull();

    expect(
      ['2', '5', '10', '15'].map((count) => button(fixture, `Start a timer for ${count} min`)),
    ).not.toContain(undefined);
    expect(page(fixture).querySelector('[data-testid="own-timer"]')).toBeNull();
    expect(
      page(fixture)
        .querySelector('[data-testid="timer-preview"] [role="timer"]')
        ?.getAttribute('aria-label'),
    ).toBe('4 min left');
  });

  it('starts a timer, keeps the screen on and stores it for a reload', async () => {
    const hold = vi.spyOn(ScreenWakeLockService.prototype, 'hold');
    const fixture = await setup();

    expect(hold).not.toHaveBeenCalled();
    const setTimeoutSpy = vi.spyOn(globalThis, 'setTimeout');
    await click(fixture, 'Start a timer for 5 min');

    expect(ownTimerLabel(fixture)).toBe('5 min left');
    // A full disc in the own timer's green, and a refresh set for the moment time runs out.
    expect(
      page(fixture)
        .querySelector('[data-testid="own-timer"] svg [fill]:not([fill="none"])')
        ?.getAttribute('fill'),
    ).toBe('#10b981');
    expect(setTimeoutSpy).toHaveBeenCalledWith(expect.any(Function), 5 * MINUTE);
    expect(button(fixture, 'Pause')).toBeDefined();
    expect(button(fixture, 'Stop timer')).toBeDefined();
    expect(page(fixture).querySelector('[data-testid="timer-preview"]')).toBeNull();
    expect(hold).toHaveBeenCalledTimes(1);
    expect(readOwnTimer(today)).toEqual({
      budgetMs: 5 * MINUTE,
      startedAtMs: NOW,
      pausedAtMs: null,
    });
  });

  it('picks up a stored timer after a reload', async () => {
    writeOwnTimer(today, {
      budgetMs: 10 * MINUTE,
      startedAtMs: NOW - 3 * MINUTE,
      pausedAtMs: null,
    });

    const fixture = await setup();

    expect(ownTimerLabel(fixture)).toBe('7 min left');
  });

  it('pauses, keeps going and stops', async () => {
    const fixture = await setup();
    await click(fixture, 'Start a timer for 10 min');

    await vi.advanceTimersByTimeAsync(2 * MINUTE);
    await click(fixture, 'Pause');
    expect(ownTimerLabel(fixture)).toBe('Paused');
    expect(readOwnTimer(today)?.pausedAtMs).toBe(NOW + 2 * MINUTE);

    await vi.advanceTimersByTimeAsync(5 * MINUTE);
    await click(fixture, 'Keep going');
    expect(ownTimerLabel(fixture)).toBe('8 min left');
    expect(readOwnTimer(today)).toEqual({
      budgetMs: 10 * MINUTE,
      startedAtMs: NOW + 5 * MINUTE,
      pausedAtMs: null,
    });

    await click(fixture, 'Stop timer');
    expect(page(fixture).querySelector('[data-testid="own-timer"]')).toBeNull();
    expect(readOwnTimer(today)).toBeNull();
    expect(button(fixture, 'Start a timer for 2 min')).toBeDefined();
  });

  it("chimes the picked sound once when time's up and offers a new timer", async () => {
    const play = vi.spyOn(TimeUpChimeService.prototype, 'play').mockImplementation(() => undefined);
    const fixture = await setup(child({ chimeSound: 'Xylophone' }));
    await click(fixture, 'Start a timer for 2 min');

    await vi.advanceTimersByTimeAsync(2 * MINUTE - 1);
    await settle(fixture);
    expect(play).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1);
    await settle(fixture);

    expect(play).toHaveBeenCalledExactlyOnceWith('Xylophone');
    expect(ownTimerLabel(fixture)).toBe("Time's up. Finish when you're ready.");
    expect(button(fixture, 'Pause')).toBeUndefined();
    await click(fixture, 'New timer');
    expect(button(fixture, 'Start a timer for 5 min')).toBeDefined();
  });

  it('stays silent and says so when a grown-up turned the sound off', async () => {
    const play = vi.spyOn(TimeUpChimeService.prototype, 'play').mockImplementation(() => undefined);
    const fixture = await setup(child({}, false));

    expect(page(fixture).textContent).toContain('A grown-up has turned the sound off.');
    expect(button(fixture, 'Listen')).toBeUndefined();

    await click(fixture, 'Start a timer for 2 min');
    await vi.advanceTimersByTimeAsync(2 * MINUTE);
    await settle(fixture);
    expect(play).not.toHaveBeenCalled();
  });

  it('saves each change as the whole preference set', async () => {
    const fixture = await setup();

    const bar = Array.from(page(fixture).querySelectorAll('[role="radio"]')).find(
      (radio) => radio.textContent?.trim() === 'Bar',
    ) as HTMLButtonElement;
    bar.click();
    await settle(fixture);
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      shape: 'Bar',
    });
    expect(bar.getAttribute('aria-checked')).toBe('true');

    await pickColour(fixture, 'Blue');
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      shape: 'Bar',
      color: 'Blue',
    });
    expect(colour(fixture, 'Blue').checked).toBe(true);
    expect(colour(fixture, 'Same as the routine').checked).toBe(false);
  });

  it('keeps the first change when a second one is made before the first is saved', async () => {
    const fixture = await setup();
    let releaseFirst!: () => void;
    const firstSaved = new Promise<void>((resolve) => (releaseFirst = resolve));
    const save = updateTimerPreferences.getMockImplementation() as (
      preferences: TimerPreferences,
    ) => Promise<CurrentUser>;
    updateTimerPreferences.mockImplementationOnce(async (preferences: TimerPreferences) => {
      await firstSaved;
      return save(preferences);
    });

    await pickColour(fixture, 'Blue');
    expect(colour(fixture, 'Blue').checked).toBe(true);
    const bar = Array.from(page(fixture).querySelectorAll('[role="radio"]')).find(
      (radio) => radio.textContent?.trim() === 'Bar',
    ) as HTMLButtonElement;
    bar.click();
    await settle(fixture);

    // The second save waits for the first, and carries both changes.
    expect(updateTimerPreferences).toHaveBeenCalledTimes(1);
    expect(bar.getAttribute('aria-checked')).toBe('true');
    expect(colour(fixture, 'Blue').checked).toBe(true);

    let releaseSecond!: () => void;
    const secondSaved = new Promise<void>((resolve) => (releaseSecond = resolve));
    updateTimerPreferences.mockImplementationOnce(async (preferences: TimerPreferences) => {
      await secondSaved;
      return save(preferences);
    });
    releaseFirst();
    await settle(fixture);
    // The first answer (Blue only) doesn't undo the Bar still being saved.
    expect(bar.getAttribute('aria-checked')).toBe('true');

    releaseSecond();
    await settle(fixture);

    expect(updateTimerPreferences).toHaveBeenCalledTimes(2);
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      color: 'Blue',
      shape: 'Bar',
    });
    expect(me()!.timerPreferences).toEqual({
      ...DEFAULT_TIMER_PREFERENCES,
      color: 'Blue',
      shape: 'Bar',
    });
  });

  it('follows the saved preferences once nothing is left to save', async () => {
    const fixture = await setup();
    await pickColour(fixture, 'Blue');

    // Changed on another device and loaded again: the page shows the account's value.
    me.set(child({ color: 'Pink' }));
    await settle(fixture);

    expect(colour(fixture, 'Pink').checked).toBe(true);
    expect(colour(fixture, 'Blue').checked).toBe(false);
  });

  it('turns the minutes and the sound on and off', async () => {
    const fixture = await setup();
    const toggle = (label: string) =>
      page(fixture).querySelector<HTMLButtonElement>(
        `button[role="switch"][aria-label="${label}"]`,
      );

    toggle('Show the minutes')!.click();
    await settle(fixture);
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      showMinutes: false,
    });

    toggle('Also on my day page')!.click();
    await settle(fixture);
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      showMinutes: false,
      chimeOnHome: true,
    });

    toggle('Play a sound')!.click();
    await settle(fixture);
    expect(updateTimerPreferences).toHaveBeenLastCalledWith({
      ...DEFAULT_TIMER_PREFERENCES,
      showMinutes: false,
      chimeOnHome: true,
      chime: false,
    });
    expect(button(fixture, 'Listen')).toBeUndefined();
    expect(toggle('Also on my day page')).toBeNull();
  });

  it('plays the chosen sound to listen to, unlocking audio first', async () => {
    const unlock = vi
      .spyOn(TimeUpChimeService.prototype, 'unlock')
      .mockImplementation(() => Promise.resolve());
    const play = vi.spyOn(TimeUpChimeService.prototype, 'play').mockImplementation(() => undefined);
    const fixture = await setup(child({ chimeSound: 'Gong' }));

    await click(fixture, 'Listen');

    expect(unlock).toHaveBeenCalled();
    expect(play).toHaveBeenCalledExactlyOnceWith('Gong');
  });

  it('shows an error when a change cannot be saved, and clears it on the next try', async () => {
    const fixture = await setup();
    updateTimerPreferences.mockRejectedValueOnce(new Error('offline'));

    await pickColour(fixture, 'Purple');
    expect(page(fixture).querySelector('[role="alert"]')?.textContent).toContain(
      "Couldn't save that. Try again.",
    );
    expect(colour(fixture, 'Purple').checked).toBe(false);

    await pickColour(fixture, 'Purple');
    expect(page(fixture).querySelector('[role="alert"]')).toBeNull();
  });

  it('unlocks the chime on a tap anywhere on the page', async () => {
    const unlock = vi
      .spyOn(TimeUpChimeService.prototype, 'unlock')
      .mockImplementation(() => Promise.resolve());
    const fixture = await setup();

    page(fixture).dispatchEvent(new Event('pointerdown'));

    expect(unlock).toHaveBeenCalledTimes(1);
  });
});
