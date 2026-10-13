import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ChildClockService } from '../../../../core/child-clock.service';
import {
  FeelingCheckIn,
  FeelingsService,
  MyFeelingsToday,
} from '../../../../core/feelings.service';
import { CurrentUser, UsersService } from '../../../../core/users.service';
import { FEELINGS_TICK_MS, FeelingsCheckIn } from './feelings-check-in';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

describe('FeelingsCheckIn', () => {
  const TODAY = '2026-10-13';
  const SAVED_AT = Date.parse('2026-10-13T13:30:00Z');

  let nowMs: number;
  let minutesNow: number;
  let clockToday: string;

  function checkIn(overrides: Partial<FeelingCheckIn> = {}): FeelingCheckIn {
    return {
      id: 'c1',
      zone: 'Yellow',
      tags: [],
      note: '',
      date: TODAY,
      time: '15:30',
      loggedBy: 'child-1',
      isSelfReport: true,
      recordedAt: new Date(SAVED_AT).toISOString(),
      ...overrides,
    };
  }

  function todayResponse(overrides: Partial<MyFeelingsToday> = {}): MyFeelingsToday {
    return {
      date: TODAY,
      promptTimes: [],
      displayMode: 'FacesAndWords',
      checkIns: [],
      ...overrides,
    };
  }

  async function setup(today: MyFeelingsToday = todayResponse()) {
    const feelings = {
      getMyToday: vi.fn(async () => today),
      checkIn: vi.fn(async () => checkIn()),
      undoCheckIn: vi.fn(async () => undefined),
    } satisfies Partial<FeelingsService>;

    await TestBed.configureTestingModule({
      imports: [FeelingsCheckIn],
      providers: [
        { provide: FeelingsService, useValue: feelings },
        {
          provide: UsersService,
          useValue: {
            ensureCurrentUser: vi.fn(async () => ({ name: { givenName: 'Emil' } }) as CurrentUser),
          },
        },
        {
          provide: ChildClockService,
          useValue: {
            now: () => nowMs,
            today: () => clockToday,
            minutesOfDay: () => minutesNow,
          } satisfies Partial<ChildClockService>,
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FeelingsCheckIn);
    await settle(fixture);
    return { fixture, feelings, el: fixture.nativeElement as HTMLElement };
  }

  function zoneButton(el: HTMLElement, zone: string): HTMLButtonElement {
    return el.querySelector(`button[data-zone="${zone}"]`) as HTMLButtonElement;
  }

  function buttonByText(el: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(el.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    ) as HTMLButtonElement;
  }

  beforeEach(() => {
    nowMs = SAVED_AT;
    minutesNow = 15 * 60 + 30;
    clockToday = TODAY;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows four labelled zone buttons with their words and says grown-ups can see it', async () => {
    const { el } = await setup();

    const buttons = el.querySelectorAll('button[data-zone]');
    expect(buttons).toHaveLength(4);
    expect(zoneButton(el, 'Green').getAttribute('aria-label')).toBe('Ready – calm, focused, okay');
    expect(zoneButton(el, 'Green').getAttribute('aria-pressed')).toBe('false');
    expect(el.textContent).toContain('Wound up');
    expect(el.textContent).toContain('Your grown-ups can see your check-ins.');
    expect(el.textContent).toContain('What do the colours mean?');
  });

  it('checks in with a zone alone: one tap plus Done', async () => {
    const { fixture, feelings, el } = await setup();

    zoneButton(el, 'Yellow').click();
    await settle(fixture);
    expect(zoneButton(el, 'Yellow').getAttribute('aria-pressed')).toBe('true');

    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(feelings.checkIn).toHaveBeenCalledWith({ zone: 'Yellow', tags: [], note: '' });
    expect(el.textContent).toContain('Thanks, Emil!');
    expect(el.querySelector('[role="status"]')?.textContent).toContain('Saved: Wound up (yellow).');
    expect(el.querySelectorAll('[data-testid="feelings-today-chips"] li')).toHaveLength(1);
  });

  it('shows only the picked zone’s words, allows at most three tags and sends a trimmed note', async () => {
    const { fixture, feelings, el } = await setup();

    zoneButton(el, 'Yellow').click();
    await settle(fixture);

    expect(buttonByText(el, 'Worried')).toBeTruthy();
    expect(buttonByText(el, 'Angry')).toBeUndefined();

    for (const tag of ['Worried', 'School', 'Friends']) {
      buttonByText(el, tag).click();
      await settle(fixture);
    }
    expect(buttonByText(el, 'Worried').getAttribute('aria-pressed')).toBe('true');
    expect(buttonByText(el, 'Home').disabled).toBe(true);

    buttonByText(el, '+ Add a note').click();
    await settle(fixture);
    const note = el.querySelector('textarea') as HTMLTextAreaElement;
    note.value = '  Maths test  ';
    note.dispatchEvent(new Event('input'));
    await settle(fixture);

    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(feelings.checkIn).toHaveBeenCalledWith({
      zone: 'Yellow',
      tags: ['Worried', 'School', 'Friends'],
      note: 'Maths test',
    });
  });

  it('undoes within the window and hides Undo once ten minutes have passed', async () => {
    vi.useFakeTimers({ toFake: ['setInterval'] });
    const { fixture, feelings, el } = await setup();

    zoneButton(el, 'Red').click();
    await settle(fixture);
    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(el.textContent).toContain('You can undo for 10 more min.');

    nowMs = SAVED_AT + 9 * 60_000;
    vi.advanceTimersByTime(FEELINGS_TICK_MS);
    await settle(fixture);
    expect(el.textContent).toContain('You can undo for 1 more min.');

    buttonByText(el, 'Undo').click();
    await settle(fixture);

    expect(feelings.undoCheckIn).toHaveBeenCalledWith('c1');
    expect(el.querySelector('[data-testid="feelings-today-chips"]')).toBeNull();
    expect(el.querySelector('[role="status"]')?.textContent).toContain('taken back');
  });

  it('closes the thanks state with Undo when the window ends', async () => {
    vi.useFakeTimers({ toFake: ['setInterval'] });
    const { fixture, el } = await setup();

    zoneButton(el, 'Green').click();
    await settle(fixture);
    buttonByText(el, 'Done').click();
    await settle(fixture);

    nowMs = SAVED_AT + 10 * 60_000 + 1;
    vi.advanceTimersByTime(FEELINGS_TICK_MS);
    await settle(fixture);

    expect(buttonByText(el, 'Undo')).toBeUndefined();
    expect(el.querySelectorAll('button[data-zone]')).toHaveLength(4);
  });

  it('highlights the card while a prompt is open, and not once the child has answered it', async () => {
    const open = await setup(todayResponse({ promptTimes: ['15:00'] }));
    expect(open.el.querySelector('[data-testid="feelings-prompt"]')).toBeTruthy();
    expect(open.el.querySelector('section')?.className).toContain('border-amber-300');

    TestBed.resetTestingModule();
    const answered = await setup(
      todayResponse({ promptTimes: ['15:00'], checkIns: [checkIn({ time: '15:10' })] }),
    );
    expect(answered.el.querySelector('[data-testid="feelings-prompt"]')).toBeNull();
  });

  it('hides the words and colour names in faces-only mode', async () => {
    const { fixture, el } = await setup(todayResponse({ displayMode: 'FacesOnly' }));

    expect(zoneButton(el, 'Green').getAttribute('aria-label')).toBe('Smiling face');
    expect(zoneButton(el, 'Green').textContent).not.toContain('Ready');
    expect(el.textContent).not.toContain('What do the colours mean?');

    zoneButton(el, 'Green').click();
    await settle(fixture);
    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(el.querySelector('[role="status"]')?.textContent?.trim()).toBe('Saved.');
  });

  it('reloads when the child’s own day has turned over', async () => {
    vi.useFakeTimers({ toFake: ['setInterval'] });
    const { fixture, feelings } = await setup();
    expect(feelings.getMyToday).toHaveBeenCalledTimes(1);

    clockToday = '2026-10-14';
    vi.advanceTimersByTime(FEELINGS_TICK_MS);
    await settle(fixture);

    expect(feelings.getMyToday).toHaveBeenCalledTimes(2);
  });

  it('shows the save error and keeps the choice', async () => {
    const { fixture, feelings, el } = await setup();
    feelings.checkIn.mockRejectedValueOnce(new Error('offline'));

    zoneButton(el, 'Blue').click();
    await settle(fixture);
    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(el.textContent).toContain('That didn’t save. Try again.');
    expect(zoneButton(el, 'Blue').getAttribute('aria-pressed')).toBe('true');
  });

  it('keeps the note and the words that still fit when the child changes the face', async () => {
    const { fixture, feelings, el } = await setup();

    zoneButton(el, 'Yellow').click();
    await settle(fixture);
    buttonByText(el, 'Worried').click();
    buttonByText(el, 'School').click();
    buttonByText(el, '+ Add a note').click();
    await settle(fixture);
    const note = el.querySelector('textarea') as HTMLTextAreaElement;
    note.value = 'Maths test';
    note.dispatchEvent(new Event('input'));
    await settle(fixture);

    zoneButton(el, 'Red').click();
    await settle(fixture);
    expect((el.querySelector('textarea') as HTMLTextAreaElement).value).toBe('Maths test');
    buttonByText(el, 'Done').click();
    await settle(fixture);

    expect(feelings.checkIn).toHaveBeenCalledWith({
      zone: 'Red',
      tags: ['School'],
      note: 'Maths test',
    });
  });

  it('says it is too late when the server’s undo window already closed, and keeps the check-in', async () => {
    const { fixture, feelings, el } = await setup();
    feelings.undoCheckIn.mockRejectedValueOnce(new HttpErrorResponse({ status: 409 }));

    zoneButton(el, 'Red').click();
    await settle(fixture);
    buttonByText(el, 'Done').click();
    await settle(fixture);
    buttonByText(el, 'Undo').click();
    await settle(fixture);

    expect(el.textContent).toContain('It’s too late to undo that one.');
    expect(buttonByText(el, 'Undo')).toBeUndefined();
    expect(el.querySelectorAll('[data-testid="feelings-today-chips"] li')).toHaveLength(1);
  });

  it('reloads once for a new day, even if the server still answers the old one', async () => {
    vi.useFakeTimers({ toFake: ['setInterval'] });
    const { fixture, feelings } = await setup();

    clockToday = '2026-10-14';
    for (let tick = 0; tick < 3; tick++) {
      vi.advanceTimersByTime(FEELINGS_TICK_MS);
      await settle(fixture);
    }

    expect(feelings.getMyToday).toHaveBeenCalledTimes(2);
  });
});
