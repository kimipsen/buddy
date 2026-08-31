import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { MedicineSchedule, MedicinesService } from '../../../../core/medicines.service';
import { ManageMedicines } from './manage-medicines';

describe('ManageMedicines', () => {
  // Mirrors the component's own (unexported) DEFAULT_COLOR constant.
  const DEFAULT_COLOR = '#f43f5e';

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return { id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' }, guardianLinkId: 'link-1', kind: 0, language: 'en', timeZoneId: 'UTC', ...overrides };
  }

  function schedule(overrides: Partial<MedicineSchedule> = {}): MedicineSchedule {
    return {
      id: 'med-1',
      childId: 'child-1',
      name: 'Amoxicillin',
      dosage: '5ml',
      icon: '💊',
      color: '#f00',
      times: ['08:00:00', '20:00:00'],
      startDate: '2026-08-01',
      endDate: null,
      isStopped: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      ...overrides
    };
  }

  function group(overrides: Partial<GroupSummary> = {}): GroupSummary {
    return { id: 'group-1', name: 'The Fam', role: 0, ...overrides };
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    medicines?: Partial<MedicinesService>;
    groups?: Partial<GroupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians
    };
    const medicinesStub: Partial<MedicinesService> = {
      listSchedules: vi.fn(async () => []),
      createSchedule: vi.fn(async (childId, request) => schedule({ id: 'med-new', childId, ...request })),
      stopSchedule: vi.fn(async () => undefined),
      shareWithGroup: vi.fn(async () => undefined),
      unshareFromGroup: vi.fn(async () => undefined),
      getSharedGroup: vi.fn(async () => null),
      ...stubs.medicines
    };
    const groupsStub: Partial<GroupsService> = {
      listMyGroups: vi.fn(async () => []),
      ...stubs.groups
    };

    await TestBed.configureTestingModule({
      imports: [ManageMedicines],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MedicinesService, useValue: medicinesStub },
        { provide: GroupsService, useValue: groupsStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageMedicines);

    return { fixture, guardians: guardiansStub, medicines: medicinesStub, groups: groupsStub };
  }

  // loadChildren chains loadSchedules and then loadSharing (itself a Promise.all of two mocked
  // calls) before the signals driving the template settle -- mirrors tasks-today.spec.ts /
  // manage-groups.spec.ts's settle(), since a single whenStable() flush isn't always enough for a
  // multi-hop stubbed service chain under zoneless change detection.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  function setInputValue(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function nameInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="medicineName"]')!;
  }

  function dosageInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[name="medicineDosage"]')!;
  }

  function submitCreateForm(compiled: HTMLElement): void {
    compiled.querySelector('form')!.dispatchEvent(new Event('submit'));
  }

  // Both the child selector and the group-share picker live outside the create-schedule <form>, so
  // scoping to outside it excludes any unrelated <select> a future in-form control might add.
  function selectsOutsideForm(compiled: HTMLElement): HTMLSelectElement[] {
    return Array.from(compiled.querySelectorAll<HTMLSelectElement>('select')).filter((select) => !select.closest('form'));
  }

  // Fills in the two fields the create form actually requires beyond its own non-empty defaults
  // (icon, color, times, startDate all start pre-filled -- see the "submit is disabled" tests).
  function fillRequiredFields(compiled: HTMLElement): void {
    setInputValue(nameInput(compiled), 'Amoxicillin');
    setInputValue(dosageInput(compiled), '5ml');
  }

  describe('loading / empty / error states', () => {
    it('shows the loading message before children resolve', async () => {
      const { fixture } = await setup();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading medicine schedules');
    });

    it('shows the no-children message when the guardian has no linked children', async () => {
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Link a child from Settings before scheduling medicine.');
      expect(compiled.querySelector('form')).toBeFalsy();
    });

    it('shows a translated error when loading children fails', async () => {
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Unable to load children.');
    });

    it('shows the empty-schedules message when the selected child has no schedules', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('No medicine schedules yet. Add one below.');
    });

    it('filters out stopped schedules from the rendered list', async () => {
      const active = schedule({ id: 'med-active', name: 'Active Med', isStopped: false });
      const stopped = schedule({ id: 'med-stopped', name: 'Stopped Med', isStopped: true });

      const { fixture } = await setup({ medicines: { listSchedules: vi.fn(async () => [active, stopped]) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Active Med');
      expect(compiled.textContent).not.toContain('Stopped Med');
    });
  });

  describe('child selection', () => {
    it('does not show a child selector when there is only one child', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect(selectsOutsideForm(fixture.nativeElement as HTMLElement)).toHaveLength(0);
    });

    it('loads the first child automatically and requests its schedules', async () => {
      const listSchedules = vi.fn(async () => []);
      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-9' })]) }, medicines: { listSchedules } });
      await settle(fixture);

      expect(listSchedules).toHaveBeenCalledWith('child-9');
    });

    it('shows a selector for multiple children and switches schedules on change', async () => {
      const childA = child({ id: 'child-a', name: { givenName: 'Ann', familyName: 'Kid' } });
      const childB = child({ id: 'child-b', name: { givenName: 'Bo', familyName: 'Kid' } });
      const listSchedules = vi
        .fn(async (): Promise<MedicineSchedule[]> => [])
        .mockResolvedValueOnce([schedule({ id: 'med-a', name: 'Med A', childId: 'child-a' })])
        .mockResolvedValueOnce([schedule({ id: 'med-b', name: 'Med B', childId: 'child-b' })]);

      const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [childA, childB]) }, medicines: { listSchedules } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Med A');

      const select = selectsOutsideForm(compiled)[0];
      select.value = 'child-b';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);

      expect(listSchedules).toHaveBeenLastCalledWith('child-b');
      expect(compiled.textContent).toContain('Med B');
      expect(compiled.textContent).not.toContain('Med A');
    });
  });

  describe('schedule rendering', () => {
    it('renders dose times without seconds, comma-separated', async () => {
      const { fixture } = await setup({
        medicines: { listSchedules: vi.fn(async () => [schedule({ times: ['08:00:00', '13:30:00'] })]) }
      });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('08:00, 13:30');
    });

    it('shows an ongoing range when there is no end date', async () => {
      const { fixture } = await setup({
        medicines: { listSchedules: vi.fn(async () => [schedule({ startDate: '2026-08-01', endDate: null })]) }
      });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('from 2026-08-01 (ongoing)');
    });

    it('shows a bounded range when there is an end date', async () => {
      const { fixture } = await setup({
        medicines: { listSchedules: vi.fn(async () => [schedule({ startDate: '2026-08-01', endDate: '2026-08-15' })]) }
      });
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('from 2026-08-01 to 2026-08-15');
    });
  });

  describe('stopping a schedule', () => {
    it('asks for confirmation before stopping, without calling the service yet', async () => {
      const stopSchedule = vi.fn(async () => undefined);
      const { fixture } = await setup({ medicines: { listSchedules: vi.fn(async () => [schedule()]), stopSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Stop')!.click();
      fixture.detectChanges();

      expect(compiled.textContent).toContain('Stop this schedule?');
      expect(stopSchedule).not.toHaveBeenCalled();
    });

    it('cancels the stop request without calling the service', async () => {
      const stopSchedule = vi.fn(async () => undefined);
      const { fixture } = await setup({ medicines: { listSchedules: vi.fn(async () => [schedule()]), stopSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Stop')!.click();
      fixture.detectChanges();
      findButtonByText(compiled, 'Cancel')!.click();
      fixture.detectChanges();

      expect(compiled.textContent).not.toContain('Stop this schedule?');
      expect(findButtonByText(compiled, 'Stop')).toBeTruthy();
      expect(stopSchedule).not.toHaveBeenCalled();
    });

    it('stops the schedule, reloads the list, and closes the confirmation on success', async () => {
      const listSchedules = vi
        .fn(async (): Promise<MedicineSchedule[]> => [])
        .mockResolvedValueOnce([schedule({ id: 'med-1', name: 'Amoxicillin' })])
        .mockResolvedValueOnce([]);
      const stopSchedule = vi.fn(async () => undefined);

      const { fixture, medicines } = await setup({ medicines: { listSchedules, stopSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Stop')!.click();
      fixture.detectChanges();
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);

      expect(medicines.stopSchedule).toHaveBeenCalledWith('child-1', 'med-1');
      expect(listSchedules).toHaveBeenCalledTimes(2);
      expect(compiled.textContent).not.toContain('Stop this schedule?');
      expect(compiled.textContent).toContain('No medicine schedules yet');
    });

    it('shows a translated error and keeps the confirmation open when stopping fails', async () => {
      const stopSchedule = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({ medicines: { listSchedules: vi.fn(async () => [schedule({ id: 'med-1' })]), stopSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Stop')!.click();
      fixture.detectChanges();
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to stop this medicine schedule.');
      // Only a successful stop clears confirmingStopScheduleId -- on failure the confirm/cancel
      // buttons stay put rather than silently reverting to the plain "Stop" button.
      expect(compiled.textContent).toContain('Stop this schedule?');
      expect(findButtonByText(compiled, 'Confirm')?.disabled).toBe(false);
    });

    it('clears a prior stop error on Cancel', async () => {
      const stopSchedule = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({ medicines: { listSchedules: vi.fn(async () => [schedule({ id: 'med-1' })]), stopSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Stop')!.click();
      fixture.detectChanges();
      findButtonByText(compiled, 'Confirm')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Unable to stop this medicine schedule.');

      findButtonByText(compiled, 'Cancel')!.click();
      fixture.detectChanges();
      expect(compiled.textContent).not.toContain('Unable to stop this medicine schedule.');
      expect(compiled.textContent).not.toContain('Stop this schedule?');
    });
  });

  describe('creating a schedule', () => {
    it('disables submit until name and dosage are both filled in (icon/color/dates start pre-filled)', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const submit = compiled.querySelector<HTMLButtonElement>('button[type="submit"]')!;
      expect(submit.disabled).toBe(true);

      setInputValue(nameInput(compiled), 'Amoxicillin');
      fixture.detectChanges();
      expect(submit.disabled).toBe(true);

      setInputValue(dosageInput(compiled), '5ml');
      fixture.detectChanges();
      expect(submit.disabled).toBe(false);
    });

    it('submits the create request with exactly the entered/defaulted values and reloads the list', async () => {
      const listSchedules = vi.fn(async () => []);
      const { fixture, medicines } = await setup({ medicines: { listSchedules } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      fillRequiredFields(compiled);
      fixture.detectChanges();
      submitCreateForm(compiled);
      await settle(fixture);

      expect(medicines.createSchedule).toHaveBeenCalledWith('child-1', {
        name: 'Amoxicillin',
        dosage: '5ml',
        icon: '💊',
        color: DEFAULT_COLOR,
        times: ['08:00:00'],
        startDate: todayIsoDate(),
        endDate: null
      });
      expect(listSchedules).toHaveBeenCalledTimes(2);
    });

    it('sends a trimmed explicit end date when one is entered', async () => {
      const { fixture, medicines } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      fillRequiredFields(compiled);
      setInputValue(compiled.querySelector<HTMLInputElement>('input[name="medicineEndDate"]')!, '2026-09-15');
      fixture.detectChanges();
      submitCreateForm(compiled);
      await settle(fixture);

      const [, request] = (medicines.createSchedule as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(request.endDate).toBe('2026-09-15');
    });

    it('resets the form to its defaults after a successful submit', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      fillRequiredFields(compiled);
      fixture.detectChanges();
      submitCreateForm(compiled);
      await settle(fixture);

      expect(nameInput(compiled).value).toBe('');
      expect(dosageInput(compiled).value).toBe('');
      expect(compiled.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    });

    it('shows a translated error and keeps the entered values when creation fails', async () => {
      const createSchedule = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({ medicines: { createSchedule } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      fillRequiredFields(compiled);
      fixture.detectChanges();
      submitCreateForm(compiled);
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to create the medicine schedule.');
      expect(nameInput(compiled).value).toBe('Amoxicillin');
      expect(compiled.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(false);
    });

    it('adds and removes dose-time fields, hiding the remove control while only one remains', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelectorAll('app-time-select')).toHaveLength(1);
      expect(findButtonByText(compiled, 'Remove')).toBeFalsy();

      findButtonByText(compiled, '+ Add another time')!.click();
      fixture.detectChanges();
      expect(compiled.querySelectorAll('app-time-select')).toHaveLength(2);
      expect(Array.from(compiled.querySelectorAll('button')).filter((b) => b.textContent?.trim() === 'Remove')).toHaveLength(2);

      Array.from(compiled.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Remove')!.click();
      fixture.detectChanges();
      expect(compiled.querySelectorAll('app-time-select')).toHaveLength(1);
      expect(findButtonByText(compiled, 'Remove')).toBeFalsy();
    });

    it('wires an edited second dose time through to the submitted times list, seconds appended', async () => {
      const { fixture, medicines } = await setup();
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, '+ Add another time')!.click();
      fixture.detectChanges();

      const secondPicker = compiled.querySelectorAll('app-time-select')[1];
      const timeInput = secondPicker.querySelector<HTMLInputElement>('input[type="time"]')!;
      timeInput.value = '14:15';
      timeInput.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      fillRequiredFields(compiled);
      fixture.detectChanges();
      submitCreateForm(compiled);
      await settle(fixture);

      const [, request] = (medicines.createSchedule as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(request.times).toEqual(['08:00:00', '14:15:00']);
    });
  });

  describe('group sharing', () => {
    it('shows the no-groups message when the guardian manages no groups and nothing is shared', async () => {
      const { fixture } = await setup();
      await settle(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'You need a group you manage before you can share this child’s medicine schedules.'
      );
    });

    it('only offers groups the guardian owns or administers, not plain member groups', async () => {
      const owner = group({ id: 'g-owner', name: 'Owned', role: 0 });
      const admin = group({ id: 'g-admin', name: 'Administered', role: 1 });
      const member = group({ id: 'g-member', name: 'Just a member', role: 2 });

      const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [owner, admin, member]) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Owned');
      expect(compiled.textContent).toContain('Administered');
      expect(compiled.textContent).not.toContain('Just a member');
    });

    it('disables the share button until a target group is chosen', async () => {
      const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [group()]) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const shareButton = findButtonByText(compiled, 'Share')!;
      expect(shareButton.disabled).toBe(true);

      const select = selectsOutsideForm(compiled)[0];
      select.value = 'group-1';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      expect(shareButton.disabled).toBe(false);
    });

    it('shares with the selected group, using its name from the manageable-groups list', async () => {
      const { fixture, medicines } = await setup({ groups: { listMyGroups: vi.fn(async () => [group({ id: 'group-1', name: 'The Fam' })]) } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const select = selectsOutsideForm(compiled)[0];
      select.value = 'group-1';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      findButtonByText(compiled, 'Share')!.click();
      await settle(fixture);

      expect(medicines.shareWithGroup).toHaveBeenCalledWith('child-1', 'group-1');
      expect(compiled.textContent).toContain('Shared with');
      expect(compiled.textContent).toContain('The Fam');
      expect(selectsOutsideForm(compiled)).toHaveLength(0);
    });

    it('shows a translated error and stays in the picker state when sharing fails', async () => {
      const shareWithGroup = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({ groups: { listMyGroups: vi.fn(async () => [group()]) }, medicines: { shareWithGroup } });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      const select = selectsOutsideForm(compiled)[0];
      select.value = 'group-1';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      findButtonByText(compiled, 'Share')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to share with this group.');
      expect(selectsOutsideForm(compiled)).toHaveLength(1);
    });

    it('unshares from the currently shared group and returns to the picker', async () => {
      const { fixture, medicines } = await setup({
        groups: { listMyGroups: vi.fn(async () => [group({ id: 'group-1', name: 'The Fam' })]) },
        medicines: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'The Fam' })) }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Shared with');

      findButtonByText(compiled, 'Unshare')!.click();
      await settle(fixture);

      expect(medicines.unshareFromGroup).toHaveBeenCalledWith('child-1', 'group-1');
      expect(compiled.textContent).not.toContain('Shared with');
      expect(selectsOutsideForm(compiled)).toHaveLength(1);
    });

    it('shows a translated error and stays shared when unsharing fails', async () => {
      const unshareFromGroup = vi.fn(async () => Promise.reject(new Error('boom')));
      const { fixture } = await setup({
        medicines: {
          getSharedGroup: vi.fn(async () => ({ groupId: 'group-1', groupName: 'The Fam' })),
          unshareFromGroup
        }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      findButtonByText(compiled, 'Unshare')!.click();
      await settle(fixture);

      expect(compiled.textContent).toContain('Unable to unshare from this group.');
      expect(compiled.textContent).toContain('Shared with');
    });

    // The real MedicinesService.getSharedGroup (see medicines.service.ts) only ever resolves a
    // fully-populated { groupId, groupName } pair or null -- but the component reads groupId and
    // groupName as two independent optional fields off whatever it gets back, and the template
    // falls back to the raw id (`sharedGroupName() ?? groupId`) when the name is missing. Stubbing
    // past the service's own guard exercises that template fallback directly.
    it('falls back to the raw group id when a shared group has no resolved name', async () => {
      const { fixture } = await setup({
        medicines: { getSharedGroup: vi.fn(async () => ({ groupId: 'group-9', groupName: null }) as unknown as { groupId: string; groupName: string }) }
      });
      await settle(fixture);

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Shared with');
      expect(compiled.textContent).toContain('group-9');
    });
  });
});
