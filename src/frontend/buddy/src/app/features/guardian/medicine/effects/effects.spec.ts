import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { MedicineEffectNote, MedicinesService } from '../../../../core/medicines.service';
import { GuardianMedicineEffects } from './effects';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Anderson' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'Europe/Copenhagen',
  } as ChildSummary;
}

function note(date: string, overrides: Partial<MedicineEffectNote> = {}): MedicineEffectNote {
  return {
    date,
    effects: [{ area: 'Focus', level: 'Good' }],
    sideEffects: [],
    woreOffAt: null,
    note: '',
    regimen: [{ medicineId: 'med-1', name: 'Ritalin', dosage: '10 mg', doses: [] }],
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

describe('GuardianMedicineEffects', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(
    options: { children?: ChildSummary[]; medicines?: Partial<MedicinesService> } = {},
  ) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 2, 15, 9, 0));

    const guardians: Partial<GuardiansService> = {
      listMyChildren: vi.fn(
        async () => options.children ?? [child('c1', 'Alex'), child('c2', 'Sam')],
      ),
    };
    const medicines: Partial<MedicinesService> = {
      listEffectNotes: vi.fn(async () => [note('2026-03-14'), note('2026-03-15')]),
      listDoses: vi.fn(async () => []),
      logEffectNote: vi.fn(async (_c: string, date: string) =>
        note(date, { effects: [{ area: 'Mood', level: 'Okay' }] }),
      ),
      clearEffectNote: vi.fn(async () => undefined),
      ...options.medicines,
    };

    await TestBed.configureTestingModule({
      imports: [GuardianMedicineEffects],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardians },
        { provide: MedicinesService, useValue: medicines },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianMedicineEffects);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, medicines };
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  it('loads 14 days for the list and 92 for the summary, and opens today in the form', async () => {
    const { compiled, medicines } = await setup();

    expect(medicines.listEffectNotes).toHaveBeenCalledWith('c1', '2026-03-02', '2026-03-15');
    expect(medicines.listEffectNotes).toHaveBeenCalledWith('c1', '2025-12-13', '2026-03-15');
    expect(compiled.querySelectorAll('app-effect-days li')).toHaveLength(14);
    expect(compiled.querySelector('app-effect-periods h4')?.textContent?.trim()).toBe(
      'Ritalin 10 mg',
    );
    expect(button(compiled, 'Clear day')).toBeDefined();
  });

  it('reloads both ranges for another child', async () => {
    const { fixture, compiled, medicines } = await setup();

    const select = compiled.querySelector<HTMLSelectElement>('#effectsChildId')!;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(medicines.listEffectNotes).toHaveBeenCalledWith('c2', '2026-03-02', '2026-03-15');
    expect(medicines.listEffectNotes).toHaveBeenCalledWith('c2', '2025-12-13', '2026-03-15');
  });

  it('pages the day list back by 14 days and forward again', async () => {
    const { fixture, compiled, medicines } = await setup();

    expect(button(compiled, 'Next 14 days →')).toBeUndefined();
    button(compiled, '← Previous 14 days')!.click();
    await settle(fixture);

    expect(medicines.listEffectNotes).toHaveBeenCalledWith('c1', '2026-02-16', '2026-03-01');
    button(compiled, 'Next 14 days →')!.click();
    await settle(fixture);
    expect(button(compiled, 'Next 14 days →')).toBeUndefined();
  });

  it('moves the form’s day into the window when paging, so it never edits an unloaded day', async () => {
    const { fixture, compiled } = await setup();
    const formDate = () => compiled.querySelector<HTMLInputElement>('app-date-select input')!.value;

    button(compiled, '← Previous 14 days')!.click();
    await settle(fixture);
    expect(formDate()).toBe('2026-03-01');

    button(compiled, 'Next 14 days →')!.click();
    await settle(fixture);
    expect(formDate()).toBe('2026-03-15');
  });

  it('opens a day from the list in the form', async () => {
    const { fixture, compiled } = await setup();

    compiled.querySelector<HTMLButtonElement>('button[aria-label="Open Sat, Mar 14"]')!.click();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('app-date-select input')!.value).toBe(
      '2026-03-14',
    );
  });

  it('limits the form to today and falls back to today for a typed future day', async () => {
    const { fixture, compiled, medicines } = await setup({
      medicines: { listEffectNotes: vi.fn(async () => []) },
    });
    const input = compiled.querySelector<HTMLInputElement>('app-date-select input')!;
    expect(input.getAttribute('max')).toBe('2026-03-15');

    input.value = '2026-03-20';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(medicines.listDoses).not.toHaveBeenCalledWith('c1', '2026-03-20', '2026-03-20');
    expect(medicines.listEffectNotes).not.toHaveBeenCalledWith('c1', '2026-03-07', '2026-03-20');
  });

  it('shows a saved day in the list and refreshes the summary', async () => {
    const { fixture, compiled, medicines } = await setup({
      medicines: { listEffectNotes: vi.fn(async () => []) },
    });
    const callsBefore = vi.mocked(medicines.listEffectNotes!).mock.calls.length;

    const focus = compiled.querySelector('[role="radiogroup"][aria-label="Mood"]')!;
    Array.from(focus.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Okay')!
      .click();
    await settle(fixture);
    button(compiled, 'Save day')!.click();
    await settle(fixture);

    expect(compiled.querySelector('app-effect-days li')?.textContent).toContain('Mood: Okay');
    expect(vi.mocked(medicines.listEffectNotes!).mock.calls.length).toBe(callsBefore + 1);
  });

  it('hides the form when the notes fail to load, so a save can’t overwrite them', async () => {
    const { compiled } = await setup({
      medicines: { listEffectNotes: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    expect(compiled.querySelector('app-effect-note-form')).toBeNull();
    expect(compiled.textContent).toContain('Unable to load the effect notes.');
  });

  it('says so when no child is linked', async () => {
    const { compiled } = await setup({ children: [] });

    expect(compiled.textContent).toContain(
      'Link a child from Settings before rating their medicine.',
    );
  });
});
