import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { MedicineEffectNote, MedicinesService } from '../../../../../core/medicines.service';
import { EffectInclusion } from './effect-inclusion';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function note(
  date: string,
  regimen: { id: string; name: string; excluded?: boolean }[],
): MedicineEffectNote {
  return {
    date,
    effects: [],
    sideEffects: [],
    woreOffAt: null,
    note: '',
    regimen: regimen.map(({ id, name, excluded = false }) => ({
      medicineId: id,
      name,
      dosage: '1',
      doses: [],
      isExcludedFromEffects: excluded,
    })),
    loggedBy: 'guardian-1',
  };
}

describe('EffectInclusion', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  async function setup(notes: MedicineEffectNote[], setEffectInclusion = vi.fn()) {
    await TestBed.configureTestingModule({
      imports: [EffectInclusion],
      providers: [{ provide: MedicinesService, useValue: { setEffectInclusion } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(EffectInclusion);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('notes', notes);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const switches = () => Array.from(element.querySelectorAll('button[role="switch"]'));
    return { fixture, element, switches, changed, setEffectInclusion };
  }

  it('shows nothing when no note names a medicine', async () => {
    const { element } = await setup([note('2026-03-02', [])]);

    expect(element.textContent?.trim()).toBe('');
  });

  it('lists each medicine once, sorted, switched on unless left out', async () => {
    const { element, switches } = await setup([
      note('2026-03-02', [
        { id: 'v', name: 'Vitamin D', excluded: true },
        { id: 'r', name: 'Ritalin' },
      ]),
      note('2026-03-03', [{ id: 'r', name: 'Ritalin' }]),
    ]);

    expect(element.textContent).toContain('Medicines in this summary');
    expect(switches().map((s) => s.getAttribute('aria-label'))).toEqual([
      'Include Ritalin in the summary',
      'Include Vitamin D in the summary',
    ]);
    expect(switches().map((s) => s.getAttribute('aria-checked'))).toEqual(['true', 'false']);
  });

  it('saves the new value and tells the page to reload', async () => {
    const setEffectInclusion = vi.fn().mockResolvedValue(undefined);
    const { fixture, switches, changed } = await setup(
      [note('2026-03-02', [{ id: 'v', name: 'Vitamin D', excluded: true }])],
      setEffectInclusion,
    );

    (switches()[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(setEffectInclusion).toHaveBeenCalledWith('child-1', 'v', true);
    expect(changed).toHaveBeenCalledTimes(1);
  });

  it('shows an error and does not reload when saving fails', async () => {
    const setEffectInclusion = vi.fn().mockRejectedValue(new Error('offline'));
    const { fixture, element, switches, changed } = await setup(
      [note('2026-03-02', [{ id: 'r', name: 'Ritalin' }])],
      setEffectInclusion,
    );

    (switches()[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(setEffectInclusion).toHaveBeenCalledWith('child-1', 'r', false);
    expect(changed).not.toHaveBeenCalled();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Could not save the change. Try again.',
    );
  });

  it('clears the error when the guardian tries again', async () => {
    const setEffectInclusion = vi
      .fn()
      .mockRejectedValueOnce(new Error('offline'))
      .mockReturnValueOnce(new Promise(() => undefined));
    const { fixture, element, switches } = await setup(
      [note('2026-03-02', [{ id: 'r', name: 'Ritalin' }])],
      setEffectInclusion,
    );

    (switches()[0] as HTMLButtonElement).click();
    await settle(fixture);
    expect(element.querySelector('[role="alert"]')).not.toBeNull();

    // The retry is still in flight, so only the start of the save can have cleared the error.
    (switches()[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('disables the switches while a save is in flight', async () => {
    let finish!: () => void;
    const setEffectInclusion = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)));
    const { fixture, switches } = await setup(
      [
        note('2026-03-02', [
          { id: 'r', name: 'Ritalin' },
          { id: 'v', name: 'Vitamin D' },
        ]),
      ],
      setEffectInclusion,
    );

    (switches()[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(switches().every((s) => (s as HTMLButtonElement).disabled)).toBe(true);

    finish();
    await settle(fixture);

    expect(switches().some((s) => (s as HTMLButtonElement).disabled)).toBe(false);
  });
});
