import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { MedicineEffectNote, MedicinesService } from '../../../../../core/medicines.service';
import { EffectNoteForm } from './effect-note-form';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function savedNote(overrides: Partial<MedicineEffectNote> = {}): MedicineEffectNote {
  return {
    date: '2026-03-02',
    effects: [{ area: 'Focus', level: 'Good' }],
    sideEffects: [{ kind: 'ReducedAppetite', severity: 'Moderate' }],
    woreOffAt: '15:30',
    note: 'Skipped lunch',
    regimen: [
      {
        medicineId: 'med-1',
        name: 'Ritalin',
        dosage: '10 mg',
        doses: [{ time: '07:30', status: 'Taken' }],
      },
    ],
    loggedBy: 'guardian-1',
    ...overrides,
  };
}

describe('EffectNoteForm', () => {
  async function setup(
    options: { note?: MedicineEffectNote | null; service?: Partial<MedicinesService> } = {},
  ) {
    const service: Partial<MedicinesService> = {
      listDoses: vi.fn(async () => [
        {
          medicineId: 'med-1',
          name: 'Ritalin',
          dosage: '15 mg',
          icon: 'pill',
          color: '#f00',
          date: '2026-03-02',
          time: '07:30:00',
          status: 'Pending' as const,
        },
      ]),
      logEffectNote: vi.fn(async (_c: string, date: string) => savedNote({ date })),
      clearEffectNote: vi.fn(async () => undefined),
      ...options.service,
    };

    await TestBed.configureTestingModule({
      imports: [EffectNoteForm],
      providers: [{ provide: MedicinesService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(EffectNoteForm);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('date', '2026-03-02');
    fixture.componentRef.setInput('note', options.note ?? null);

    const saved = vi.fn();
    const cleared = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.componentInstance.cleared.subscribe(cleared);

    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    return { fixture, compiled, service, saved, cleared };
  }

  function group(compiled: HTMLElement, label: string): HTMLElement {
    const found = compiled.querySelector<HTMLElement>(`[role="radiogroup"][aria-label="${label}"]`);
    if (!found) {
      throw new Error(`No radio group labelled ${label}`);
    }
    return found;
  }

  function checked(compiled: HTMLElement, label: string): string | undefined {
    return group(compiled, label).querySelector('[aria-checked="true"]')?.textContent?.trim();
  }

  function choose(compiled: HTMLElement, label: string, option: string): void {
    const button = Array.from(group(compiled, label).querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === option,
    );
    button!.click();
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  it('starts a new day unrated, previews the regimen from the day’s doses and hints at backdating', async () => {
    const { compiled, service } = await setup();

    expect(service.listDoses).toHaveBeenCalledWith('child-1', '2026-03-02', '2026-03-02');
    expect(compiled.textContent).toContain('Ritalin 15 mg');
    expect(compiled.textContent).toContain('not marked');
    expect(compiled.textContent).toContain('A day rated for the first time gets the dosage');
    expect(checked(compiled, 'Focus')).toBe('Not rated');
    expect(checked(compiled, 'Less appetite')).toBe('Not rated');
    expect(button(compiled, 'Clear day')).toBeUndefined();
  });

  it('says a day with no active medicine is a baseline day', async () => {
    const { compiled } = await setup({ service: { listDoses: vi.fn(async () => []) } });

    expect(compiled.textContent).toContain('No medicine this day. It counts as a baseline day.');
  });

  it('saves only what was rated, keeping None apart from unrated', async () => {
    const { fixture, compiled, service, saved } = await setup();

    choose(compiled, 'Focus', 'Good');
    choose(compiled, 'Trouble falling asleep', 'None');
    choose(compiled, 'Mood', 'Poor');
    choose(compiled, 'Mood', 'Not rated');
    const note = compiled.querySelector('textarea')!;
    note.value = ' Great in maths ';
    note.dispatchEvent(new Event('input'));
    await settle(fixture);

    button(compiled, 'Save day')!.click();
    await settle(fixture);

    expect(service.logEffectNote).toHaveBeenCalledWith('child-1', '2026-03-02', {
      effects: [{ area: 'Focus', level: 'Good' }],
      sideEffects: [{ kind: 'TroubleFallingAsleep', severity: 'None' }],
      woreOffAt: null,
      note: 'Great in maths',
    });
    expect(saved).toHaveBeenCalledWith(expect.objectContaining({ date: '2026-03-02' }));
    expect(compiled.querySelector('[role="status"]')?.textContent?.trim()).toBe('Saved.');
  });

  it('refuses to save an empty day without calling the API', async () => {
    const { fixture, compiled, service } = await setup();

    button(compiled, 'Save day')!.click();
    await settle(fixture);

    expect(service.logEffectNote).not.toHaveBeenCalled();
    expect(compiled.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Rate something, give a wear-off time or write a note before saving.',
    );
  });

  it('fills the form from a saved note and shows its stored regimen without fetching doses', async () => {
    const { compiled, service } = await setup({ note: savedNote() });

    expect(service.listDoses).not.toHaveBeenCalled();
    expect(checked(compiled, 'Focus')).toBe('Good');
    expect(checked(compiled, 'Less appetite')).toBe('Moderate');
    expect(compiled.querySelector('textarea')!.value).toBe('Skipped lunch');
    expect(compiled.textContent).toContain('Ritalin 10 mg');
    expect(compiled.textContent).toContain('taken');
    expect(compiled.textContent).not.toContain('A day rated for the first time');
  });

  it('opens the less common side effects when one is rated, and toggles them', async () => {
    const { fixture, compiled } = await setup({
      note: savedNote({ sideEffects: [{ kind: 'Tics', severity: 'Mild' }] }),
    });

    expect(checked(compiled, 'Tics')).toBe('Mild');
    button(compiled, 'Fewer side effects')!.click();
    await settle(fixture);
    expect(compiled.querySelector('[aria-label="Tics"]')).toBeNull();
    button(compiled, 'More side effects')!.click();
    await settle(fixture);
    expect(compiled.querySelector('[aria-label="Tics"]')).not.toBeNull();
  });

  it('clears a saved day', async () => {
    const { fixture, compiled, service, cleared } = await setup({ note: savedNote() });

    button(compiled, 'Clear day')!.click();
    await settle(fixture);

    expect(service.clearEffectNote).toHaveBeenCalledWith('child-1', '2026-03-02');
    expect(cleared).toHaveBeenCalledWith('2026-03-02');
  });

  it('shows an error when saving fails', async () => {
    const { fixture, compiled } = await setup({
      service: { logEffectNote: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });

    choose(compiled, 'Focus', 'Okay');
    await settle(fixture);
    button(compiled, 'Save day')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Unable to save this day.',
    );
  });
});
