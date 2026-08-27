import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { DosesToday } from './doses-today';

describe('DosesToday', () => {
  const today = todayIsoDate();

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Sam', familyName: 'Kid' },
      guardianLinkId: 'link-1',
      kind: 0,
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides
    };
  }

  function dose(overrides: Partial<MedicineDoseOccurrence> = {}): MedicineDoseOccurrence {
    return {
      medicineId: 'med-1',
      name: 'Amoxicillin',
      dosage: '5ml',
      icon: '💊',
      color: '#f00',
      date: today,
      time: '08:00:00',
      status: 0,
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    medicines?: Partial<MedicinesService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians
    };
    const medicinesStub: Partial<MedicinesService> = {
      listDoses: vi.fn(async () => []),
      setDoseStatus: vi.fn(),
      ...stubs.medicines
    };

    await TestBed.configureTestingModule({
      imports: [DosesToday],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MedicinesService, useValue: medicinesStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(DosesToday);

    return { fixture, guardians: guardiansStub, medicines: medicinesStub };
  }

  // loadDoses chains more than one await (listMyChildren, then a Promise.all of per-child
  // listDoses calls) before the signals driving the template settle. Per docs/testing.md, this
  // app runs zoneless and whenStable() does not resolve on a plain mocked Promise, so a macrotask
  // flush is used instead, repeated to cover any depth of chained awaits (including the toggle
  // handler's own await).
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    for (let i = 0; i < 10; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function findButton(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  it('shows the loading spinner while doses are loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the no-children state when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings to track their medicine.');
  });

  it('shows the empty state once loading finishes with no doses', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No medicine scheduled for today.');
  });

  it('shows the translated error message when loading doses fails', async () => {
    const { fixture } = await setup({ medicines: { listDoses: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s medicine doses.');
  });

  it('shows the translated error message when listing children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s medicine doses.');
  });

  it('renders a pending dose with mark-taken and skip actions, sorted by time', async () => {
    const earlyDose = dose({ medicineId: 'med-early', name: 'Vitamin D', time: '07:00:00', status: 0 });
    const lateDose = dose({ medicineId: 'med-late', name: 'Ibuprofen', time: '20:00:00', status: 0 });

    const { fixture } = await setup({ medicines: { listDoses: vi.fn(async () => [lateDose, earlyDose]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const items = Array.from(compiled.querySelectorAll('li'));
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('Vitamin D');
    expect(items[1].textContent).toContain('Ibuprofen');
    expect(compiled.textContent).toContain('Mark taken');
    expect(compiled.textContent).toContain('Skip');
  });

  it('does not show the child name when only one child is linked', async () => {
    const { fixture } = await setup({ medicines: { listDoses: vi.fn(async () => [dose()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Sam');
  });

  it('shows the child name alongside each dose when multiple children are linked', async () => {
    const childA = child({ id: 'child-a', name: { givenName: 'Alice', familyName: 'A' } });
    const childB = child({ id: 'child-b', name: { givenName: 'Bob', familyName: 'B' } });
    const doseA = dose({ medicineId: 'med-a', name: 'Amoxicillin', time: '08:00:00' });
    const doseB = dose({ medicineId: 'med-b', name: 'Cough syrup', time: '09:00:00' });

    const listDoses = vi.fn(async (childId: string) => (childId === 'child-a' ? [doseA] : [doseB]));

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [childA, childB]) },
      medicines: { listDoses }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Alice');
    expect(compiled.textContent).toContain('Bob');
    expect(listDoses).toHaveBeenCalledWith('child-a', today, today);
    expect(listDoses).toHaveBeenCalledWith('child-b', today, today);
  });

  it('renders a taken dose with a status pill and undo action, and a skipped dose likewise', async () => {
    const takenDose = dose({ medicineId: 'med-taken', name: 'Taken med', status: 1 });
    const skippedDose = dose({ medicineId: 'med-skipped', name: 'Skipped med', time: '09:00:00', status: 2 });

    const { fixture } = await setup({ medicines: { listDoses: vi.fn(async () => [takenDose, skippedDose]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Taken');
    expect(compiled.textContent).toContain('Skipped');
    const undoButtons = Array.from(compiled.querySelectorAll('button')).filter((b) => b.textContent?.trim() === 'Undo');
    expect(undoButtons).toHaveLength(2);
  });

  it('marks a pending dose taken and asserts the exact setDoseStatus call args', async () => {
    const pendingDose = dose({ medicineId: 'med-1', date: today, time: '08:00:00', status: 0 });
    const updated: MedicineDoseOccurrence = { ...pendingDose, status: 1 };
    const setDoseStatus = vi.fn(async () => updated);

    const { fixture, medicines } = await setup({
      medicines: { listDoses: vi.fn(async () => [pendingDose]), setDoseStatus }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButton(compiled, 'Mark taken')!.click();
    await settle(fixture);

    expect(medicines.setDoseStatus).toHaveBeenCalledWith('child-1', 'med-1', today, '08:00:00', 1);
    expect(compiled.textContent).toContain('Taken');
  });

  it('skips a pending dose and asserts the exact setDoseStatus call args', async () => {
    const pendingDose = dose({ medicineId: 'med-2', date: today, time: '12:00:00', status: 0 });
    const updated: MedicineDoseOccurrence = { ...pendingDose, status: 2 };
    const setDoseStatus = vi.fn(async () => updated);

    const { fixture, medicines } = await setup({
      medicines: { listDoses: vi.fn(async () => [pendingDose]), setDoseStatus }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButton(compiled, 'Skip')!.click();
    await settle(fixture);

    expect(medicines.setDoseStatus).toHaveBeenCalledWith('child-1', 'med-2', today, '12:00:00', 2);
    expect(compiled.textContent).toContain('Skipped');
  });

  it('undoes a taken dose back to pending and asserts the exact setDoseStatus call args', async () => {
    const takenDose = dose({ medicineId: 'med-3', date: today, time: '10:00:00', status: 1 });
    const updated: MedicineDoseOccurrence = { ...takenDose, status: 0 };
    const setDoseStatus = vi.fn(async () => updated);

    const { fixture, medicines } = await setup({
      medicines: { listDoses: vi.fn(async () => [takenDose]), setDoseStatus }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButton(compiled, 'Undo')!.click();
    await settle(fixture);

    expect(medicines.setDoseStatus).toHaveBeenCalledWith('child-1', 'med-3', today, '10:00:00', 0);
    expect(compiled.textContent).toContain('Mark taken');
  });

  it('disables the action buttons for the dose being saved while the update is in flight', async () => {
    const pendingDose = dose({ medicineId: 'med-4', status: 0 });
    let resolveUpdate!: (value: MedicineDoseOccurrence) => void;
    const setDoseStatus = vi.fn(
      () =>
        new Promise<MedicineDoseOccurrence>((resolve) => {
          resolveUpdate = resolve;
        })
    );

    const { fixture } = await setup({
      medicines: { listDoses: vi.fn(async () => [pendingDose]), setDoseStatus }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const markTaken = findButton(compiled, 'Mark taken')!;
    markTaken.click();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(findButton(compiled, 'Mark taken')?.disabled).toBe(true);
    expect(findButton(compiled, 'Skip')?.disabled).toBe(true);

    resolveUpdate({ ...pendingDose, status: 1 });
    await settle(fixture);

    expect(compiled.textContent).toContain('Taken');
  });

  it('keeps the dose list visible alongside the error message when updating a dose fails', async () => {
    const pendingDose = dose({ medicineId: 'med-5', name: 'Still pending', status: 0 });
    const setDoseStatus = vi.fn(async () => Promise.reject(new Error('boom')));

    const { fixture } = await setup({
      medicines: { listDoses: vi.fn(async () => [pendingDose]), setDoseStatus }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButton(compiled, 'Mark taken')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to update this dose.');
    expect(compiled.textContent).toContain('Still pending');
  });
});
