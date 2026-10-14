import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../core/date-utils';
import { createSession, next, start } from '../../../core/homework-session';
import { readHomeworkSession, writeHomeworkSession } from '../../../core/homework-storage';
import { CurrentUser, UsersService } from '../../../core/users.service';
import { ChildHomework } from './child-homework';

describe('ChildHomework', () => {
  const today = todayIsoDate();
  const NOW = Date.parse(`${today}T15:00:00Z`);
  const MIN = 60_000;

  const me = (overrides: Partial<CurrentUser> = {}): CurrentUser => ({
    id: 'child-1',
    email: { value: 'emil@buddy.test', isVerified: true },
    userName: 'emil',
    name: { givenName: 'Emil', familyName: 'Kid' },
    timeZoneId: 'UTC',
    language: 'en',
    showVisualTimers: true,
    homeworkChunks: { focusMinutes: 10, breakMinutes: 2 },
    ...overrides,
  });

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  async function setup(user: CurrentUser | Error = me()) {
    await TestBed.configureTestingModule({
      imports: [ChildHomework],
      providers: [
        provideRouter([]),
        {
          provide: UsersService,
          useValue: {
            ensureCurrentUser: vi.fn(async () => {
              if (user instanceof Error) {
                throw user;
              }
              return user;
            }),
            timeZoneId: signal('UTC').asReadonly(),
            profileTimeZoneId: signal<string | null>(null).asReadonly(),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildHomework);
    await settle(fixture);
    return fixture;
  }

  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function el(fixture: ComponentFixture<unknown>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function text(fixture: ComponentFixture<unknown>): string {
    return (el(fixture).textContent ?? '').replace(/\s+/g, ' ');
  }

  function button(fixture: ComponentFixture<unknown>, label: string): HTMLButtonElement {
    const found = Array.from(el(fixture).querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === label || b.textContent?.trim().endsWith(label),
    );
    if (!found) {
      throw new Error(`button "${label}" not found in: ${text(fixture)}`);
    }
    return found;
  }

  function hasButton(fixture: ComponentFixture<unknown>, label: string): boolean {
    return Array.from(el(fixture).querySelectorAll('button')).some(
      (b) => b.textContent?.trim() === label,
    );
  }

  async function typeTitle(fixture: ComponentFixture<unknown>, index: number, value: string) {
    const input = el(fixture).querySelectorAll<HTMLInputElement>('input[type="text"]')[index];
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  async function chooseEstimate(fixture: ComponentFixture<unknown>, index: number, label: string) {
    const select = el(fixture).querySelectorAll<HTMLSelectElement>('select')[index];
    const option = Array.from(select.options).findIndex((o) => o.textContent?.trim() === label);
    select.selectedIndex = option;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);
  }

  function storeSession(advance = 0) {
    let session = start(
      createSession(
        [
          { id: 'maths', title: 'Maths', estimateMinutes: 20 },
          { id: 'reading', title: 'Reading', estimateMinutes: 10 },
        ],
        { focusMinutes: 10, breakMinutes: 2 },
        today,
      ),
      NOW,
    );
    for (let i = 0; i < advance; i++) {
      session = next(session, NOW);
    }
    writeHomeworkSession(session);
  }

  it('plans homework with a live preview of the split, using the guardian lengths', async () => {
    const fixture = await setup();

    expect(text(fixture)).toContain("Write down tonight's homework.");
    expect(text(fixture)).toContain('Focus 10 min · Break 2 min');
    // One part of 20 min by default: two chunks and one break.
    expect(text(fixture)).toContain('Focus chunks: 2 · Breaks: 1 · About 22 min');
    expect(button(fixture, 'Start').disabled).toBe(true);

    await typeTitle(fixture, 0, 'Maths');
    expect(button(fixture, 'Start').disabled).toBe(false);

    button(fixture, 'Add more homework').click();
    await settle(fixture);
    expect(el(fixture).querySelectorAll('[data-testid="homework-part"]')).toHaveLength(2);
    expect(button(fixture, 'Start').disabled).toBe(true);

    await typeTitle(fixture, 1, 'Reading');
    await chooseEstimate(fixture, 1, '5 min');
    expect(text(fixture)).toContain('Focus chunks: 3 · Breaks: 2 · About 29 min');

    button(fixture, 'Remove homework 2').click();
    await settle(fixture);
    expect(el(fixture).querySelectorAll('[data-testid="homework-part"]')).toHaveLength(1);
    expect(hasButton(fixture, 'Remove homework 1')).toBe(false);
  });

  it('falls back to 15/5 when the user does not load', async () => {
    const fixture = await setup(new Error('offline'));

    expect(text(fixture)).toContain('Focus 15 min · Break 5 min');
  });

  it('allows at most six parts', async () => {
    const fixture = await setup();

    for (let i = 0; i < 5; i++) {
      button(fixture, 'Add more homework').click();
      await settle(fixture);
    }

    expect(el(fixture).querySelectorAll('[data-testid="homework-part"]')).toHaveLength(6);
    expect(hasButton(fixture, '+ Add more homework')).toBe(false);
  });

  it('starts the first chunk and stores the session', async () => {
    const fixture = await setup();
    await typeTitle(fixture, 0, '  Maths  ');

    button(fixture, 'Start').click();
    await settle(fixture);

    expect(el(fixture).querySelector('h2')?.textContent).toContain('Maths: 1 of 2');
    expect(el(fixture).querySelector('[role="timer"]')?.getAttribute('aria-label')).toBe(
      '10 min left',
    );
    expect(text(fixture)).toContain('Next: a break');
    expect(hasButton(fixture, 'Pause')).toBe(true);
    expect(hasButton(fixture, 'Done with Maths')).toBe(true);
    expect(hasButton(fixture, 'Take a break')).toBe(false);

    const stored = readHomeworkSession(today);
    expect(stored?.parts[0].title).toBe('Maths');
    expect(stored?.startedAtMs).toBe(NOW);
  });

  it('continues a stored session after a reload', async () => {
    storeSession(1);
    vi.setSystemTime(NOW + MIN);

    const fixture = await setup();

    expect(el(fixture).querySelector('[data-step]')?.getAttribute('data-step')).toBe('break');
    expect(el(fixture).querySelector('h2')?.textContent).toContain('Break');
    expect(text(fixture)).toContain('Break idea: Drink some water');
    expect(text(fixture)).toContain('Next: Maths: 2 of 2');
    expect(el(fixture).querySelector('[role="timer"]')?.getAttribute('aria-label')).toBe(
      '1 min left',
    );
  });

  it('pauses and resumes a chunk', async () => {
    storeSession();
    const fixture = await setup();

    button(fixture, 'Pause').click();
    await settle(fixture);
    expect(readHomeworkSession(today)?.pausedAtMs).toBe(NOW);
    expect(hasButton(fixture, 'Keep going')).toBe(true);

    button(fixture, 'Keep going').click();
    await settle(fixture);
    expect(readHomeworkSession(today)?.pausedAtMs).toBeNull();
    expect(hasButton(fixture, 'Pause')).toBe(true);
  });

  it("offers a break or more time when a chunk's time is up", async () => {
    storeSession();
    vi.setSystemTime(NOW + 11 * MIN);
    const fixture = await setup();

    expect(text(fixture)).toContain('Time for a break!');
    expect(hasButton(fixture, 'Pause')).toBe(false);
    expect(hasButton(fixture, 'Take a break')).toBe(true);
    expect(hasButton(fixture, 'Done with Maths')).toBe(true);

    button(fixture, 'I need more time').click();
    await settle(fixture);

    expect(el(fixture).querySelector('h2')?.textContent).toContain('Maths: 2 of 3');
    expect(text(fixture)).not.toContain('Time for a break!');
  });

  it('takes a break, goes back to work and announces each step', async () => {
    storeSession();
    vi.setSystemTime(NOW + 11 * MIN);
    const fixture = await setup();
    const announcement = () =>
      el(fixture).querySelector('[data-testid="homework-announcement"]')?.textContent?.trim();

    button(fixture, 'Take a break').click();
    await settle(fixture);
    expect(el(fixture).querySelector('h2')?.textContent).toContain('Break');
    expect(announcement()).toBe('Break time.');

    button(fixture, 'Back to work').click();
    await settle(fixture);
    expect(el(fixture).querySelector('h2')?.textContent).toContain('Maths: 2 of 2');
    expect(announcement()).toBe('Back to work: Maths: 2 of 2.');
  });

  it("offers Done with the part on a part's last chunk, so an early finish can move on", async () => {
    storeSession(2);
    const fixture = await setup();

    expect(el(fixture).querySelector('h2')?.textContent).toContain('Maths: 2 of 2');
    expect(hasButton(fixture, 'Done with Maths')).toBe(true);
    expect(hasButton(fixture, 'Take a break')).toBe(false);
  });

  it("offers only Take a break once a part's last chunk is up", async () => {
    storeSession(2);
    vi.setSystemTime(NOW + 11 * MIN);
    const fixture = await setup();

    expect(hasButton(fixture, 'Take a break')).toBe(true);
    expect(hasButton(fixture, 'Done with Maths')).toBe(false);
  });

  it('skips the rest of a part when it is done early', async () => {
    storeSession();
    const fixture = await setup();

    button(fixture, 'Done with Maths').click();
    await settle(fixture);
    button(fixture, 'Back to work').click();
    await settle(fixture);

    expect(el(fixture).querySelector('h2')?.textContent).toContain('Reading: 1 of 1');
    expect(text(fixture)).toContain('This is the last one!');
  });

  it('finishes with All done, clears the stored session and can plan again', async () => {
    storeSession(4);
    const fixture = await setup();

    button(fixture, "I'm done").click();
    await settle(fixture);

    expect(text(fixture)).toContain('All done! Great work.');
    expect(text(fixture)).toContain('Maths');
    expect(text(fixture)).toContain('Reading');
    expect(readHomeworkSession(today)).toBeNull();

    button(fixture, 'Plan more homework').click();
    await settle(fixture);
    expect(text(fixture)).toContain("Write down tonight's homework.");
  });

  it('stops homework only after a confirm', async () => {
    storeSession();
    const fixture = await setup();

    button(fixture, 'Stop homework').click();
    await settle(fixture);
    expect(text(fixture)).toContain('Stop homework? Your plan will be cleared.');

    button(fixture, 'No, keep going').click();
    await settle(fixture);
    expect(readHomeworkSession(today)).not.toBeNull();

    button(fixture, 'Stop homework').click();
    await settle(fixture);
    button(fixture, 'Yes, stop').click();
    await settle(fixture);

    expect(readHomeworkSession(today)).toBeNull();
    expect(text(fixture)).toContain("Write down tonight's homework.");
  });

  it('shows no disc or bar when the guardian turned timers off', async () => {
    storeSession();
    const fixture = await setup(me({ showVisualTimers: false }));

    expect(el(fixture).querySelector('h2')?.textContent).toContain('Maths: 1 of 2');
    expect(el(fixture).querySelector('[role="timer"]')).toBeNull();
    expect(hasButton(fixture, 'Pause')).toBe(true);
    expect(hasButton(fixture, 'Done with Maths')).toBe(true);
  });
});
