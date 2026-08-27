import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { toIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardianSummary, GuardiansService } from '../../../../core/guardians.service';
import { AssignPickupRequest, PickupOccurrence, PickupsService } from '../../../../core/pickups.service';
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
      kind: 0,
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides
    };
  }

  function guardian(overrides: Partial<GuardianSummary> = {}): GuardianSummary {
    return {
      id: 'guardian-1',
      name: { givenName: 'Gina', familyName: 'G' },
      guardianLinkId: 'link-1',
      kind: 0,
      ...overrides
    };
  }

  function occurrence(overrides: Partial<PickupOccurrence> = {}): PickupOccurrence {
    return {
      date: weekStart,
      slot: 0,
      kind: 0,
      guardianId: 'guardian-1',
      siblingChildId: null,
      playdateHostName: null,
      playdateLocation: null,
      playdateContactInfo: null,
      time: null,
      notes: null,
      assignedBy: 'guardian-1',
      ...overrides
    };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    pickups?: Partial<PickupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      listChildGuardians: vi.fn(async () => [guardian()]),
      ...stubs.guardians
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => []),
      assignPickup: vi.fn(),
      clearPickup: vi.fn(),
      ...stubs.pickups
    };

    await TestBed.configureTestingModule({
      imports: [ManagePickups],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManagePickups);

    return { fixture, guardians: guardiansStub, pickups: pickupsStub };
  }

  // loadChildren and loadForChild each chain more than one await (a Promise.all of two mocked
  // service calls) before the signals driving the template settle -- see docs/testing.md's
  // zoneless-async note. A macrotask flush reliably drains any depth of chained awaits as long as
  // nothing in the chain schedules a further macrotask itself, which is the case here.
  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function cells(fixture: ComponentFixture<unknown>): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('app-pickup-cell'));
  }

  // Cells render day-major, slot-minor (dropOff=0 then pickUp=1 per day) -- see manage-pickups.html.
  function cellAt(fixture: ComponentFixture<unknown>, dayOffset: number, slot: 0 | 1): HTMLElement {
    return cells(fixture)[dayOffset * 2 + slot];
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
    const { fixture, guardians, pickups } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings before planning pickups.');
    expect(compiled.textContent).not.toContain('Loading schedule…');
    expect(guardians.listChildGuardians).not.toHaveBeenCalled();
    expect(pickups.listSchedule).not.toHaveBeenCalled();
  });

  it('shows the translated load error when fetching children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the pickup schedule.');
  });

  it('shows the translated load error when fetching the schedule for the child fails', async () => {
    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load the pickup schedule.');
  });

  it('fetches the child\'s guardians and schedule for a fixed 7-day window starting today', async () => {
    const { fixture, guardians, pickups } = await setup();
    await settle(fixture);

    expect(guardians.listChildGuardians).toHaveBeenCalledWith('child-1');
    expect(pickups.listSchedule).toHaveBeenCalledWith('child-1', weekStart, weekEnd);

    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(7);
  });

  it('renders every slot as "Not planned" when nothing is scheduled', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('app-pickup-cell')).toHaveLength(14);
    expect(compiled.textContent?.match(/Not planned/g)).toHaveLength(14);
  });

  it('keys an occurrence by date and slot, so it only shows in its own cell', async () => {
    const dropOff = occurrence({ date: weekStart, slot: 0, guardianId: 'guardian-1' });

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

  it('renders a child picker and switches the schedule when there is more than one child', async () => {
    const childA = child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } });
    const childB = child({ id: 'child-2', name: { givenName: 'Robin', familyName: 'Kid' } });
    const listChildGuardians = vi.fn(async () => [guardian()]);
    const listSchedule = vi.fn(async () => []);

    const { fixture, guardians, pickups } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [childA, childB]), listChildGuardians },
      pickups: { listSchedule }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const picker = compiled.querySelector('select') as HTMLSelectElement;
    expect(picker).toBeTruthy();
    expect(Array.from(picker.options).map((option) => option.textContent?.trim())).toEqual(['Sam', 'Robin']);

    listChildGuardians.mockClear();
    listSchedule.mockClear();

    picker.value = 'child-2';
    picker.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(guardians.listChildGuardians).toHaveBeenCalledWith('child-2');
    expect(pickups.listSchedule).toHaveBeenCalledWith('child-2', weekStart, weekEnd);
  });

  it('excludes the selected child from the sibling list passed to each cell', async () => {
    const childA = child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } });
    const childB = child({ id: 'child-2', name: { givenName: 'Robin', familyName: 'Kid' } });

    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [childA, childB]) } });
    await settle(fixture);

    // childA (Sam) is selected by default (first child returned) -- only childB (Robin) should be
    // offered as a sibling to assign pickup/drop-off to.
    const cell = cellAt(fixture, 0, 0);
    cell.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    const kindSelect = cell.querySelectorAll('select')[0] as HTMLSelectElement;
    kindSelect.value = kindSelect.options[2].value; // sibling option
    kindSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const siblingSelect = cell.querySelectorAll('select')[1] as HTMLSelectElement;
    const siblingOptionLabels = Array.from(siblingSelect.options)
      .map((option) => option.textContent?.trim())
      .filter((label) => label && label !== 'Choose a sibling');
    expect(siblingOptionLabels).toEqual(['Robin']);
  });

  describe('assigning a pickup', () => {
    it('sends the exact childId/date/slot/request to the service and shows the result once it resolves', async () => {
      const assignedOccurrence = occurrence({ date: weekStart, slot: 0, kind: 0, guardianId: 'guardian-1' });
      const assignPickup = vi.fn(async () => assignedOccurrence);

      const { fixture, pickups } = await setup({ pickups: { assignPickup } });
      await settle(fixture);

      const cell = cellAt(fixture, 0, 0);
      const notPlannedButton = cell.querySelector<HTMLButtonElement>('button')!;
      expect(notPlannedButton.textContent).toContain('Not planned');
      notPlannedButton.click();
      fixture.detectChanges();

      // Default kind is "guardian" (GUARDIAN=0), so only the guardian picker needs a value.
      const guardianSelect = cell.querySelectorAll('select')[1] as HTMLSelectElement;
      guardianSelect.value = 'guardian-1';
      guardianSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      const saveButton = Array.from(cell.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Save')!;
      expect(saveButton.hasAttribute('disabled')).toBe(false);
      saveButton.click();
      await settle(fixture);

      const expectedRequest: AssignPickupRequest = {
        kind: 0,
        guardianId: 'guardian-1',
        siblingChildId: null,
        playdateHostName: null,
        playdateLocation: null,
        playdateContactInfo: null,
        time: null,
        notes: null
      };
      expect(pickups.assignPickup).toHaveBeenCalledWith('child-1', weekStart, 0, expectedRequest);

      expect(cellAt(fixture, 0, 0).textContent).toContain('Gina');
      expect(cellAt(fixture, 0, 0).textContent).not.toContain('Not planned');
    });

    it('routes the day and slot of the specific cell that was edited, not just the first one', async () => {
      const assignedOccurrence = occurrence({ date: isoDateOffset(2), slot: 1, kind: 1 });
      const assignPickup = vi.fn(async () => assignedOccurrence);

      const { fixture, pickups } = await setup({ pickups: { assignPickup } });
      await settle(fixture);

      const cell = cellAt(fixture, 2, 1);
      cell.querySelector<HTMLButtonElement>('button')!.click();
      fixture.detectChanges();

      // Switch to "goes alone" (SELF_ESCORT=1), which needs no further picker to become saveable.
      const kindSelect = cell.querySelectorAll('select')[0] as HTMLSelectElement;
      kindSelect.value = kindSelect.options[1].value;
      kindSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      Array.from(cell.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Save')!.click();
      await settle(fixture);

      expect(pickups.assignPickup).toHaveBeenCalledWith(
        'child-1',
        isoDateOffset(2),
        1,
        expect.objectContaining({ kind: 1 })
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

      const guardianSelect = cell.querySelectorAll('select')[1] as HTMLSelectElement;
      guardianSelect.value = 'guardian-1';
      guardianSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      Array.from(cell.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Save')!.click();
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

  describe('clearing a pickup', () => {
    it('sends the exact childId/date/slot to the service and reverts the cell once it resolves', async () => {
      const existing = occurrence({ date: isoDateOffset(1), slot: 1, kind: 0, guardianId: 'guardian-1' });
      const clearPickup = vi.fn(async () => undefined);

      const { fixture, pickups } = await setup({
        pickups: { listSchedule: vi.fn(async () => [existing]), clearPickup }
      });
      await settle(fixture);

      const cell = cellAt(fixture, 1, 1);
      expect(cell.textContent).toContain('Gina');

      const clearButton = Array.from(cell.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Clear')!;
      clearButton.click();
      await settle(fixture);

      expect(pickups.clearPickup).toHaveBeenCalledWith('child-1', isoDateOffset(1), 1);
      expect(cellAt(fixture, 1, 1).textContent).toContain('Not planned');
    });

    it('shows the translated error and keeps the assignment when clearing fails', async () => {
      const existing = occurrence({ date: weekStart, slot: 0, kind: 0, guardianId: 'guardian-1' });
      const clearPickup = vi.fn(async () => Promise.reject(new Error('boom')));

      const { fixture } = await setup({
        pickups: { listSchedule: vi.fn(async () => [existing]), clearPickup }
      });
      await settle(fixture);

      const cell = cellAt(fixture, 0, 0);
      const clearButton = Array.from(cell.querySelectorAll('button')).find((button) => button.textContent?.trim() === 'Clear')!;
      clearButton.click();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to update this slot.');
      expect(cellAt(fixture, 0, 0).textContent).toContain('Gina');
    });
  });
});
