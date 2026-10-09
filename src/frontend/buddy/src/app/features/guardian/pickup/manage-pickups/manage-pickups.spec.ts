import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { BabysittersService, ChildBabysitter } from '../../../../core/babysitters.service';
import { toIsoDate } from '../../../../core/date-utils';
import {
  ChildSummary,
  GuardianSummary,
  GuardiansService,
} from '../../../../core/guardians.service';
import {
  AssignPickupRequest,
  PickupOccurrence,
  PickupsService,
} from '../../../../core/pickups.service';
import { ManagePickups } from './manage-pickups';

describe('ManagePickups', () => {
  // Mirrors buildWeek()'s own date math (today + offset, in local time) so expectations don't
  // depend on knowing "today" from outside the test.
  function isoDateOffset(offsetDays: number): string {
    const today = new Date();
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() + offsetDays);
    return toIsoDate(date);
  }

  const weekStart = isoDateOffset(0);
  const weekEnd = isoDateOffset(6);

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Sam', familyName: 'Kid' },
      guardianLinkId: 'link-1',
      kind: 'Parent',
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides,
    };
  }

  function guardian(overrides: Partial<GuardianSummary> = {}): GuardianSummary {
    return {
      id: 'guardian-1',
      name: { givenName: 'Gina', familyName: 'G' },
      guardianLinkId: 'link-1',
      kind: 'Parent',
      ...overrides,
    };
  }

  function occurrence(overrides: Partial<PickupOccurrence> = {}): PickupOccurrence {
    return {
      assignee: { kind: 0, guardianId: 'guardian-1' },
      date: weekStart,
      slot: 'DropOff',
      time: null,
      notes: '',
      assignedBy: 'guardian-1',
      ...overrides,
    };
  }

  interface Stubs {
    babysitters?: Partial<BabysittersService>;
    guardians?: Partial<GuardiansService>;
    pickups?: Partial<PickupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      listChildGuardians: vi.fn(async () => [guardian()]),
      ...stubs.guardians,
    };
    const babysittersStub: Partial<BabysittersService> = {
      listForChild: vi.fn(async () => []),
      ...stubs.babysitters,
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => []),
      assignPickup: vi.fn(),
      clearPickup: vi.fn(),
      ...stubs.pickups,
    };

    await TestBed.configureTestingModule({
      imports: [ManagePickups],
      providers: [
        provideRouter([]),
        { provide: BabysittersService, useValue: babysittersStub },
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManagePickups);

    return {
      fixture,
      babysitters: babysittersStub,
      guardians: guardiansStub,
      pickups: pickupsStub,
    };
  }

  // The children resource and then the selected child's schedule resource (a Promise.all of two
  // mocked service calls) each resolve before the template settles, and the second only starts
  // once change detection has run the first's result through -- see docs/testing.md's
  // zoneless-async note. A few macrotask rounds, each followed by detectChanges(), drain both.
  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    fixture.detectChanges();

    for (let i = 0; i < 3; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function cells(fixture: ComponentFixture<unknown>): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('app-pickup-cell'));
  }

  // Cells render day-major, slot-minor (dropOff=0 then pickUp=1 per day) -- see manage-pickups.html.
  function cellAt(fixture: ComponentFixture<unknown>, dayOffset: number, slot: 0 | 1): HTMLElement {
    return cells(fixture)[dayOffset * 2 + slot];
  }

  // The "kind" picker inside a pickup-cell's edit form is an `app-segmented-control`: a
  // `role="radiogroup"` of `role="radio"` buttons, not a native `<select>` -- see
  // shared/segmented-control/segmented-control.html.
  function selectKind(cell: HTMLElement, label: string): void {
    const button = Array.from(
      cell.querySelectorAll<HTMLButtonElement>('[role="radiogroup"] button[role="radio"]'),
    ).find((candidate) => candidate.textContent?.trim() === label);
    expect(button, `kind option "${label}" not found`).toBeTruthy();
    button!.click();
  }

  function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (reason?: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  it('shows the loading message before the initial load settles', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading schedule…');
  });

  it('shows the "no children" message and skips fetching a schedule when the guardian has no linked children', async () => {
    const { fixture, guardians, pickups } = await setup({
      guardians: { listMyChildren: vi.fn(async () => []) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings before planning pickups.');
    expect(compiled.textContent).not.toContain('Loading schedule…');
    expect(guardians.listChildGuardians).not.toHaveBeenCalled();
    expect(pickups.listSchedule).not.toHaveBeenCalled();
  });

  it('shows the translated load error when fetching children fails', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the pickup schedule.');
  });

  it('shows the translated load error when fetching the schedule for the child fails', async () => {
    const { fixture } = await setup({
      pickups: { listSchedule: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the pickup schedule.');
  });

  it("fetches the child's guardians and schedule for a fixed 7-day window starting today", async () => {
    const { fixture, guardians, pickups } = await setup();
    await settle(fixture);

    expect(guardians.listChildGuardians).toHaveBeenCalledWith('child-1');
    expect(pickups.listSchedule).toHaveBeenCalledWith('child-1', weekStart, weekEnd);

    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(7);
  });

  it("fetches the child's babysitters and offers them in a cell's babysitter picker", async () => {
    const anna: ChildBabysitter = {
      guardianId: 'guardian-1',
      id: 'b-1',
      name: 'Anna',
      contactInfo: '',
    };
    const { fixture, babysitters } = await setup({
      babysitters: { listForChild: vi.fn(async () => [anna]) },
    });
    await settle(fixture);

    expect(babysitters.listForChild).toHaveBeenCalledWith('child-1');

    const compiled = fixture.nativeElement as HTMLElement;
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Not planned')!
      .click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll<HTMLButtonElement>('button[role="radio"]'))
      .find((b) => b.textContent?.trim() === 'Babysitter')!
      .click();
    await settle(fixture);

    const options = Array.from(compiled.querySelectorAll('select option')).map((o) =>
      o.textContent?.trim(),
    );
    expect(options).toContain('Anna');
  });

  it('still renders the schedule when only the babysitter list fails to load', async () => {
    const { fixture } = await setup({
      babysitters: { listForChild: vi.fn(async () => Promise.reject(new Error('boom'))) },
      pickups: { listSchedule: vi.fn(async () => [occurrence()]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Unable to load the pickup schedule.');
    expect(compiled.textContent).toContain('Gina');
  });

  it('renders every slot as "Not planned" when nothing is scheduled', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('app-pickup-cell')).toHaveLength(14);
    expect(compiled.textContent?.match(/Not planned/g)).toHaveLength(14);
  });

  it('keys an occurrence by date and slot, so it only shows in its own cell', async () => {
    const dropOff = occurrence({
      date: weekStart,
      slot: 'DropOff',
      assignee: { kind: 0, guardianId: 'guardian-1' },
    });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [dropOff]) } });
    await settle(fixture);

    const dropOffCell = cellAt(fixture, 0, 0);
    const pickUpCell = cellAt(fixture, 0, 1);
    const otherDayCell = cellAt(fixture, 1, 0);

    expect(dropOffCell.textContent).toContain('Gina');
    expect(dropOffCell.textContent).not.toContain('Not planned');
    expect(pickUpCell.textContent).toContain('Not planned');
    expect(otherDayCell.textContent).toContain('Not planned');
  });

  it('does not render the child picker when the guardian has only one child', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('select')).toBeNull();
  });

  it('renders a child picker in service (name) order and switches the schedule when there is more than one child', async () => {
    const childA = child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } });
    const childB = child({ id: 'child-2', name: { givenName: 'Robin', familyName: 'Kid' } });
    const listChildGuardians = vi.fn(async () => [guardian()]);
    const listSchedule = vi.fn(async () => []);

    const { fixture, guardians, pickups } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [childB, childA]), listChildGuardians },
      pickups: { listSchedule },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const picker = compiled.querySelector('select') as HTMLSelectElement;
    expect(picker).toBeTruthy();
    expect(Array.from(picker.options).map((option) => option.textContent?.trim())).toEqual([
      'Robin',
      'Sam',
    ]);
    // The first child in that order is selected by default.
    expect(picker.value).toBe('child-2');

    listChildGuardians.mockClear();
    listSchedule.mockClear();

    picker.value = 'child-1';
    picker.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(guardians.listChildGuardians).toHaveBeenCalledWith('child-1');
    expect(pickups.listSchedule).toHaveBeenCalledWith('child-1', weekStart, weekEnd);
  });

  it('excludes the selected child from the sibling list passed to each cell', async () => {
    const childA = child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } });
    const childB = child({ id: 'child-2', name: { givenName: 'Robin', familyName: 'Kid' } });

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [childB, childA]) },
    });
    await settle(fixture);

    // childB (Robin) is selected by default (first in the list) -- only childA (Sam) should be
    // offered as a sibling to assign pickup/drop-off to.
    const cell = cellAt(fixture, 0, 0);
    cell.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    selectKind(cell, 'A sibling');
    fixture.detectChanges();

    const siblingSelect = cell.querySelectorAll('select')[0] as HTMLSelectElement;
    const siblingOptionLabels = Array.from(siblingSelect.options)
      .map((option) => option.textContent?.trim())
      .filter((label) => label && label !== 'Choose a sibling');
    expect(siblingOptionLabels).toEqual(['Sam']);
  });

  describe('assigning a pickup', () => {
    it('sends the exact childId/date/slot/request to the service and shows the result once it resolves', async () => {
      const assignedOccurrence = occurrence({
        assignee: { kind: 0, guardianId: 'guardian-1' },
        date: weekStart,
        slot: 'DropOff',
      });
      const assignPickup = vi.fn(async () => assignedOccurrence);

      const { fixture, pickups } = await setup({ pickups: { assignPickup } });
      await settle(fixture);

      const cell = cellAt(fixture, 0, 0);
      const notPlannedButton = cell.querySelector<HTMLButtonElement>('button')!;
      expect(notPlannedButton.textContent).toContain('Not planned');
      notPlannedButton.click();
      fixture.detectChanges();

      // Default kind is "guardian" (GUARDIAN=0), so only the guardian picker needs a value.
      const guardianSelect = cell.querySelectorAll('select')[0] as HTMLSelectElement;
      guardianSelect.value = 'guardian-1';
      guardianSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      const saveButton = Array.from(cell.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === 'Save',
      )!;
      expect(saveButton.hasAttribute('disabled')).toBe(false);
      saveButton.click();
      await settle(fixture);

      const expectedRequest: AssignPickupRequest = {
        assignee: { kind: 0, guardianId: 'guardian-1' },
        time: null,
        notes: '',
      };
      expect(pickups.assignPickup).toHaveBeenCalledWith(
        'child-1',
        weekStart,
        'DropOff',
        expectedRequest,
      );

      expect(cellAt(fixture, 0, 0).textContent).toContain('Gina');
      expect(cellAt(fixture, 0, 0).textContent).not.toContain('Not planned');
    });

    it('routes the day and slot of the specific cell that was edited, not just the first one', async () => {
      const assignedOccurrence = occurrence({
        assignee: { kind: 1 },
        date: isoDateOffset(2),
        slot: 'PickUp',
      });
      const assignPickup = vi.fn(async () => assignedOccurrence);

      const { fixture, pickups } = await setup({ pickups: { assignPickup } });
      await settle(fixture);

      const cell = cellAt(fixture, 2, 1);
      cell.querySelector<HTMLButtonElement>('button')!.click();
      fixture.detectChanges();

      // Switch to "goes alone" (SELF_ESCORT=1), which needs no further picker to become saveable.
      selectKind(cell, 'Goes alone');
      fixture.detectChanges();

      Array.from(cell.querySelectorAll('button'))
        .find((button) => button.textContent?.trim() === 'Save')!
        .click();
      await settle(fixture);

      expect(pickups.assignPickup).toHaveBeenCalledWith(
        'child-1',
        isoDateOffset(2),
        'PickUp',
        expect.objectContaining({ assignee: { kind: 1 } }),
      );
    });

    it('disables the cell while the assignment is saving, and re-enables it if the save fails', async () => {
      const pending = deferred<PickupOccurrence>();
      const assignPickup = vi.fn(() => pending.promise);

      const { fixture } = await setup({ pickups: { assignPickup } });
      await settle(fixture);

      const cell = cellAt(fixture, 0, 0);
      cell.querySelector<HTMLButtonElement>('button')!.click();
      fixture.detectChanges();

      const guardianSelect = cell.querySelectorAll('select')[0] as HTMLSelectElement;
      guardianSelect.value = 'guardian-1';
      guardianSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      Array.from(cell.querySelectorAll('button'))
        .find((button) => button.textContent?.trim() === 'Save')!
        .click();
      fixture.detectChanges();

      // Editing closed immediately on save(); while the request is in flight the cell falls back to
      // its (still unplanned) summary button, disabled via the `saving` input.
      const notPlannedButton = cell.querySelector<HTMLButtonElement>('button')!;
      expect(notPlannedButton.disabled).toBe(true);

      pending.reject(new Error('boom'));
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to update this slot.');
      const buttonAfterFailure = cellAt(fixture, 0, 0).querySelector<HTMLButtonElement>('button')!;
      expect(buttonAfterFailure.disabled).toBe(false);
      expect(buttonAfterFailure.textContent).toContain('Not planned');
    });
  });

  describe('a save that resolves after switching child', () => {
    async function startSaveThenSwitchChild() {
      // Child A comes first, so it is selected by default.
      const childA = child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } });
      const childB = child({ id: 'child-2', name: { givenName: 'Tove', familyName: 'Kid' } });
      const pending = deferred<PickupOccurrence>();
      const assignPickup = vi.fn(() => pending.promise);

      const { fixture } = await setup({
        guardians: { listMyChildren: vi.fn(async () => [childA, childB]) },
        pickups: { assignPickup },
      });
      await settle(fixture);

      // Start a "goes alone" save on child A's first cell and leave it in flight.
      const cell = cellAt(fixture, 0, 0);
      cell.querySelector<HTMLButtonElement>('button')!.click();
      fixture.detectChanges();
      selectKind(cell, 'Goes alone');
      fixture.detectChanges();
      Array.from(cell.querySelectorAll('button'))
        .find((button) => button.textContent?.trim() === 'Save')!
        .click();
      fixture.detectChanges();
      expect(assignPickup).toHaveBeenCalledWith('child-1', weekStart, 'DropOff', expect.anything());

      // Switch to child B and let its (empty) schedule finish loading first.
      const picker = (fixture.nativeElement as HTMLElement).querySelector(
        'select#selectedChildId',
      ) as HTMLSelectElement;
      picker.value = 'child-2';
      picker.dispatchEvent(new Event('change'));
      await settle(fixture);

      return { fixture, pending };
    }

    it("doesn't disable child B's cell while child A's save is in flight", async () => {
      const { fixture } = await startSaveThenSwitchChild();

      expect(cellAt(fixture, 0, 0).querySelector<HTMLButtonElement>('button')!.disabled).toBe(
        false,
      );
    });

    it("keeps child A's result out of child B's grid", async () => {
      const { fixture, pending } = await startSaveThenSwitchChild();

      pending.resolve(occurrence({ assignee: { kind: 1 }, date: weekStart, slot: 'DropOff' }));
      await settle(fixture);

      expect(cellAt(fixture, 0, 0).textContent).toContain('Not planned');
    });

    it("doesn't show child A's save error while child B is selected", async () => {
      const { fixture, pending } = await startSaveThenSwitchChild();

      pending.reject(new Error('boom'));
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(
        'Unable to update this slot.',
      );
    });
  });

  describe('clearing a pickup', () => {
    it('sends the exact childId/date/slot to the service and reverts the cell once it resolves', async () => {
      const existing = occurrence({
        assignee: { kind: 0, guardianId: 'guardian-1' },
        date: isoDateOffset(1),
        slot: 'PickUp',
      });
      const clearPickup = vi.fn(async () => undefined);

      const { fixture, pickups } = await setup({
        pickups: { listSchedule: vi.fn(async () => [existing]), clearPickup },
      });
      await settle(fixture);

      const cell = cellAt(fixture, 1, 1);
      expect(cell.textContent).toContain('Gina');

      const clearButton = Array.from(cell.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === 'Clear',
      )!;
      clearButton.click();
      await settle(fixture);

      expect(pickups.clearPickup).toHaveBeenCalledWith('child-1', isoDateOffset(1), 'PickUp');
      expect(cellAt(fixture, 1, 1).textContent).toContain('Not planned');
    });

    it('shows the translated error and keeps the assignment when clearing fails', async () => {
      const existing = occurrence({
        assignee: { kind: 0, guardianId: 'guardian-1' },
        date: weekStart,
        slot: 'DropOff',
      });
      const clearPickup = vi.fn(async () => Promise.reject(new Error('boom')));

      const { fixture } = await setup({
        pickups: { listSchedule: vi.fn(async () => [existing]), clearPickup },
      });
      await settle(fixture);

      const cell = cellAt(fixture, 0, 0);
      const clearButton = Array.from(cell.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === 'Clear',
      )!;
      clearButton.click();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to update this slot.');
      expect(cellAt(fixture, 0, 0).textContent).toContain('Gina');
    });
  });
});
