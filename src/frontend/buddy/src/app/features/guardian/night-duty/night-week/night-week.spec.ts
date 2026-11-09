import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { Night, NightDutyService, NightEntry } from '../../../../core/night-duty.service';
import { NightPeople } from '../night-people';
import { NightWeek } from './night-week';

describe('NightWeek', () => {
  const people: NightPeople = {
    guardians: [
      {
        id: 'mom',
        name: { givenName: 'Sara', familyName: 'Holm' },
        callingName: 'Mom',
        kind: 'Parent',
      },
      {
        id: 'dad',
        name: { givenName: 'Jonas', familyName: 'Holm' },
        callingName: 'Dad',
        kind: 'Parent',
      },
    ] as NightPeople['guardians'],
    babysitters: [],
  };

  const mom: NightEntry = { responsible: { kind: 0, guardianId: 'mom' }, note: '' };
  const dad: NightEntry = { responsible: { kind: 0, guardianId: 'dad' }, note: '' };

  // Monday 2026-11-09 .. Sunday 2026-11-15.
  function week(): Night[] {
    return [
      { date: '2026-11-09', status: { kind: 2, entry: mom, source: 'Pattern', scheduled: null } },
      { date: '2026-11-10', status: { kind: 2, entry: mom, source: 'Override', scheduled: dad } },
      {
        date: '2026-11-11',
        status: {
          kind: 2,
          entry: { ...dad, note: 'Screen night' },
          source: 'Pattern',
          scheduled: null,
        },
      },
      { date: '2026-11-12', status: { kind: 1, scheduled: mom } },
      {
        date: '2026-11-13',
        status: {
          kind: 2,
          entry: { responsible: { kind: 2, hostName: 'Grandma Lis', location: '' }, note: '' },
          source: 'Override',
          scheduled: null,
        },
      },
      {
        date: '2026-11-14',
        status: {
          kind: 2,
          entry: { responsible: { kind: 3 }, note: 'Movie night' },
          source: 'Pattern',
          scheduled: null,
        },
      },
      { date: '2026-11-15', status: { kind: 0 } },
    ];
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function tile(root: HTMLElement, date: string): HTMLElement {
    const element = root.querySelector<HTMLElement>(`[data-night="${date}"]`);
    expect(element, date).toBeTruthy();
    return element!;
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement {
    const found = Array.from(root.querySelectorAll('button')).find((b) =>
      b.textContent?.trim().includes(text),
    );
    expect(found, text).toBeTruthy();
    return found!;
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 10, 9, 12));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(service: Partial<Record<keyof NightDutyService, unknown>> = {}) {
    const listNights = vi.fn(async () => week());
    const setOverrides = vi.fn(async () => []);
    const clearOverrides = vi.fn(async () => undefined);
    const stub = { listNights, setOverrides, clearOverrides, ...service };

    await TestBed.configureTestingModule({
      imports: [NightWeek],
      providers: [{ provide: NightDutyService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(NightWeek);
    fixture.componentRef.setInput('childId', 'emil');
    fixture.componentRef.setInput('people', people);
    fixture.componentRef.setInput('today', '2026-11-09');
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, ...stub };
  }

  it('shows the seven nights from tonight with who, notes and changes', async () => {
    const { compiled, listNights } = await setup();

    expect(listNights).toHaveBeenCalledWith('emil', '2026-11-09', '2026-11-15');
    expect(compiled.textContent).toContain('Nights Nov 9 – Nov 15');
    expect(tile(compiled, '2026-11-09').textContent).toContain('Mom');
    expect(tile(compiled, '2026-11-09').textContent).toContain('From schedule');
    expect(tile(compiled, '2026-11-09').textContent).toContain('to Tue');

    const changed = tile(compiled, '2026-11-10').textContent;
    expect(changed).toContain('Changed');
    expect(changed).toContain('Schedule: Dad');

    expect(tile(compiled, '2026-11-11').textContent).toContain('Screen night');
    expect(tile(compiled, '2026-11-12').textContent).toContain('Nothing planned');
    expect(tile(compiled, '2026-11-12').textContent).toContain('Schedule: Mom');
    expect(tile(compiled, '2026-11-13').textContent).toContain('Sleepover: Grandma Lis');
    expect(tile(compiled, '2026-11-13').textContent).toContain('Schedule: nothing');
    expect(tile(compiled, '2026-11-14').textContent).toContain('Movie night');
    expect(tile(compiled, '2026-11-14').textContent).not.toContain('Nothing planned');
    expect(tile(compiled, '2026-11-15').textContent).toContain('Nothing planned');
    expect(tile(compiled, '2026-11-15').textContent).not.toContain('From schedule');
  });

  it('pages a week at a time', async () => {
    const { fixture, compiled, listNights } = await setup();

    button(compiled, 'Next week').click();
    await settle(fixture);
    expect(listNights).toHaveBeenLastCalledWith('emil', '2026-11-16', '2026-11-22');

    button(compiled, 'Previous week').click();
    button(compiled, 'Previous week').click();
    await settle(fixture);
    expect(listNights).toHaveBeenLastCalledWith('emil', '2026-11-02', '2026-11-08');
  });

  it('undoes a change and reloads the week', async () => {
    const { fixture, compiled, clearOverrides, listNights } = await setup();

    tile(compiled, '2026-11-10').querySelector<HTMLButtonElement>('[aria-label^="Undo"]')!.click();
    await settle(fixture);

    expect(clearOverrides).toHaveBeenCalledWith('emil', '2026-11-10', '2026-11-10');
    expect(listNights).toHaveBeenCalledTimes(2);
  });

  it('saves a night from the dialog and closes it', async () => {
    const { fixture, compiled, setOverrides, listNights } = await setup();

    tile(compiled, '2026-11-09')
      .querySelector<HTMLButtonElement>('[aria-label^="Change"]')!
      .click();
    await settle(fixture);
    expect(compiled.querySelector('[role="dialog"]')).toBeTruthy();

    const who = compiled.querySelector<HTMLSelectElement>('#night-edit-who')!;
    who.value = 'g:dad';
    who.dispatchEvent(new Event('change'));
    await settle(fixture);
    button(compiled, 'Save night').click();
    await settle(fixture);

    expect(setOverrides).toHaveBeenCalledWith('emil', [
      { date: '2026-11-09', entry: { responsible: { kind: 0, guardianId: 'dad' }, note: '' } },
    ]);
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
    expect(listNights).toHaveBeenCalledTimes(2);
  });

  it('keeps the dialog open with the error when saving fails', async () => {
    const { fixture, compiled } = await setup({
      setOverrides: vi.fn(async () => {
        throw new Error('400');
      }),
    });

    tile(compiled, '2026-11-09')
      .querySelector<HTMLButtonElement>('[aria-label^="Change"]')!
      .click();
    await settle(fixture);
    button(compiled, 'Save night').click();
    await settle(fixture);

    expect(compiled.querySelector('[role="dialog"]')?.textContent).toContain(
      'Unable to save the night.',
    );
  });

  it('switches who is on duty between two nights, each keeping its note', async () => {
    const { fixture, compiled, setOverrides } = await setup();

    button(compiled, 'Switch two nights').click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Select the two nights to switch.');

    tile(compiled, '2026-11-09').querySelector('button')!.click();
    tile(compiled, '2026-11-11').querySelector('button')!.click();
    // A third pick is ignored until one is deselected.
    tile(compiled, '2026-11-13').querySelector('button')!.click();
    await settle(fixture);

    button(compiled, 'Switch Mon, Nov 9 and Wed, Nov 11').click();
    await settle(fixture);

    expect(setOverrides).toHaveBeenCalledWith('emil', [
      { date: '2026-11-09', entry: { responsible: dad.responsible, note: '' } },
      { date: '2026-11-11', entry: { responsible: mom.responsible, note: 'Screen night' } },
    ]);
    expect(compiled.textContent).not.toContain('Select the two nights to switch.');
  });

  it('switching with an empty night moves the person and leaves a note-only night', async () => {
    const { fixture, compiled, setOverrides } = await setup();

    button(compiled, 'Switch two nights').click();
    await settle(fixture);
    tile(compiled, '2026-11-14').querySelector('button')!.click();
    tile(compiled, '2026-11-15').querySelector('button')!.click();
    tile(compiled, '2026-11-15').querySelector('button')!.click();
    tile(compiled, '2026-11-12').querySelector('button')!.click();
    await settle(fixture);
    button(compiled, 'Switch Sat, Nov 14 and Thu, Nov 12').click();
    await settle(fixture);

    expect(setOverrides).toHaveBeenCalledWith('emil', [
      { date: '2026-11-14', entry: { responsible: { kind: 3 }, note: 'Movie night' } },
      { date: '2026-11-12', entry: null },
    ]);
  });

  it('drops the picks of a switch when the week changes', async () => {
    const { fixture, compiled, setOverrides } = await setup();

    button(compiled, 'Switch two nights').click();
    await settle(fixture);
    tile(compiled, '2026-11-09').querySelector('button')!.click();
    await settle(fixture);
    button(compiled, 'Next week').click();
    await settle(fixture);
    // The stub returns the same nights for every week, so one more pick would make a pair if the
    // first one had been kept.
    tile(compiled, '2026-11-10').querySelector('button')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Select the two nights to switch.');

    button(compiled, 'Previous week').click();
    await settle(fixture);
    tile(compiled, '2026-11-11').querySelector('button')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Select the two nights to switch.');
    expect(setOverrides).not.toHaveBeenCalled();
  });

  it('closes the dialog on Cancel and after going back to the schedule', async () => {
    const { fixture, compiled, clearOverrides } = await setup();

    tile(compiled, '2026-11-10')
      .querySelector<HTMLButtonElement>('[aria-label^="Change"]')!
      .click();
    await settle(fixture);
    button(compiled, 'Cancel').click();
    await settle(fixture);
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();

    tile(compiled, '2026-11-10')
      .querySelector<HTMLButtonElement>('[aria-label^="Change"]')!
      .click();
    await settle(fixture);
    button(compiled, 'Back to schedule').click();
    await settle(fixture);
    expect(clearOverrides).toHaveBeenCalledWith('emil', '2026-11-10', '2026-11-10');
    expect(compiled.querySelector('[role="dialog"]')).toBeNull();
  });

  it('keeps the dialog open when going back to the schedule fails, and a new dialog starts without the old error', async () => {
    const { fixture, compiled } = await setup({
      clearOverrides: vi.fn(async () => {
        throw new Error('500');
      }),
    });

    // Undo straight from the tile: the error shows on the card.
    tile(compiled, '2026-11-10').querySelector<HTMLButtonElement>('[aria-label^="Undo"]')!.click();
    await settle(fixture);
    expect(compiled.querySelector('app-card')?.textContent).toContain('Unable to save the night.');

    tile(compiled, '2026-11-10')
      .querySelector<HTMLButtonElement>('[aria-label^="Change"]')!
      .click();
    await settle(fixture);
    const dialog = () => compiled.querySelector('[role="dialog"]');
    expect(dialog()?.textContent).not.toContain('Unable to save the night.');

    button(compiled, 'Back to schedule').click();
    await settle(fixture);
    expect(dialog()?.textContent).toContain('Unable to save the night.');
  });

  it('leaves switch mode after a switch, and stays in it with the error when the switch fails', async () => {
    const ok = await setup();
    button(ok.compiled, 'Switch two nights').click();
    await settle(ok.fixture);
    tile(ok.compiled, '2026-11-09').querySelector('button')!.click();
    tile(ok.compiled, '2026-11-11').querySelector('button')!.click();
    await settle(ok.fixture);
    button(ok.compiled, 'Switch Mon, Nov 9 and Wed, Nov 11').click();
    await settle(ok.fixture);
    expect(button(ok.compiled, 'Switch two nights').getAttribute('aria-pressed')).toBe('false');
    TestBed.resetTestingModule();

    const failing = await setup({
      setOverrides: vi.fn(async () => {
        throw new Error('400');
      }),
    });
    button(failing.compiled, 'Switch two nights').click();
    await settle(failing.fixture);
    tile(failing.compiled, '2026-11-09').querySelector('button')!.click();
    tile(failing.compiled, '2026-11-11').querySelector('button')!.click();
    await settle(failing.fixture);
    button(failing.compiled, 'Switch Mon, Nov 9 and Wed, Nov 11').click();
    await settle(failing.fixture);
    expect(button(failing.compiled, 'Cancel switch').getAttribute('aria-pressed')).toBe('true');
    expect(failing.compiled.textContent).toContain('Unable to save the night.');
  });

  it('names unknown guardians and unnamed babysitters generically', async () => {
    const { compiled } = await setup({
      listNights: vi.fn(async (): Promise<Night[]> => [
        {
          date: '2026-11-09',
          status: {
            kind: 2,
            entry: { responsible: { kind: 0, guardianId: 'gone' } },
            source: 'Override',
            scheduled: { responsible: { kind: 3 } },
          },
        },
        {
          date: '2026-11-10',
          status: {
            kind: 2,
            entry: { responsible: { kind: 1, guardianId: 'mom', babysitterId: 'b' } },
            source: 'Pattern',
            scheduled: null,
          },
        },
      ]),
    });

    expect(tile(compiled, '2026-11-09').textContent).toContain('A guardian');
    // The schedule's entry names nobody and has no note: shown as nothing.
    expect(tile(compiled, '2026-11-09').textContent).toContain('Schedule: nothing');
    expect(tile(compiled, '2026-11-10').textContent).toContain('Babysitter');
  });

  it('shows a load error', async () => {
    const { compiled } = await setup({
      listNights: vi.fn(async () => {
        throw new Error('500');
      }),
    });

    expect(compiled.textContent).toContain('Unable to load the nights.');
  });
});
