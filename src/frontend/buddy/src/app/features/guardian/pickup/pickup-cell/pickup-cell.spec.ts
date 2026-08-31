import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardianSummary } from '../../../../core/guardians.service';
import { AssignPickupRequest, PickupAssigneeKind, PickupOccurrence } from '../../../../core/pickups.service';
import { PickupCell } from './pickup-cell';

describe('PickupCell', () => {
  function guardian(id: string, givenName: string): GuardianSummary {
    return { id, name: { givenName, familyName: 'Guardian' }, guardianLinkId: `link-${id}`, kind: 1 };
  }

  function sibling(id: string, givenName: string): ChildSummary {
    return { id, name: { givenName, familyName: 'Kid' }, guardianLinkId: `link-${id}`, kind: 0, language: 'en', timeZoneId: 'UTC' };
  }

  function occurrence(overrides: Partial<PickupOccurrence> = {}): PickupOccurrence {
    return {
      date: '2026-08-26',
      slot: 0,
      kind: 0,
      guardianId: null,
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

  interface Options {
    guardians?: GuardianSummary[];
    siblings?: ChildSummary[];
    occurrence?: PickupOccurrence | null;
    disabled?: boolean;
    saving?: boolean;
  }

  async function setup(options: Options = {}) {
    await TestBed.configureTestingModule({ imports: [PickupCell] }).compileComponents();

    const fixture = TestBed.createComponent(PickupCell);
    const onAssign = vi.fn();
    const onClear = vi.fn();
    fixture.componentInstance.assign.subscribe(onAssign);
    fixture.componentInstance.clear.subscribe(onClear);

    fixture.componentRef.setInput('guardians', options.guardians ?? []);
    fixture.componentRef.setInput('siblings', options.siblings ?? []);
    if (options.occurrence !== undefined) {
      fixture.componentRef.setInput('occurrence', options.occurrence);
    }
    if (options.disabled !== undefined) {
      fixture.componentRef.setInput('disabled', options.disabled);
    }
    if (options.saving !== undefined) {
      fixture.componentRef.setInput('saving', options.saving);
    }

    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, onAssign, onClear };
  }

  // See docs/testing.md's zoneless-async note: SelectControlValueAccessor doesn't finish writing
  // an [ngValue]-bound select's initial selection within the same synchronous detectChanges() that
  // first creates it (its <option>s register with the accessor a tick later), so a macrotask flush
  // is needed before reading selectedOptions on a just-opened edit form.
  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function buttons(compiled: HTMLElement): HTMLButtonElement[] {
    return Array.from(compiled.querySelectorAll('button'));
  }

  function findButton(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return buttons(compiled).find((button) => button.textContent?.trim().includes(text));
  }

  function selects(compiled: HTMLElement): HTMLSelectElement[] {
    return Array.from(compiled.querySelectorAll('select'));
  }

  function selectByLabel(select: HTMLSelectElement, label: string): void {
    const index = Array.from(select.options).findIndex((option) => option.textContent?.trim() === label);
    expect(index, `option "${label}" not found`).toBeGreaterThanOrEqual(0);
    select.selectedIndex = index;
    select.dispatchEvent(new Event('change'));
  }

  function selectByValue(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function timeInput(compiled: HTMLElement): HTMLInputElement {
    return compiled.querySelector<HTMLInputElement>('input[type="time"]')!;
  }

  function setTime(compiled: HTMLElement, value: string): void {
    const input = timeInput(compiled);
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function setInput(compiled: HTMLElement, placeholder: string, value: string): void {
    const input = compiled.querySelector<HTMLInputElement>(`input[placeholder="${placeholder}"]`);
    expect(input, `input with placeholder "${placeholder}" not found`).toBeTruthy();
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
  }

  describe('rendering an unplanned slot', () => {
    it('shows the "not planned" placeholder and no clear button', async () => {
      const { compiled } = await setup();

      expect(compiled.textContent).toContain('Not planned');
      expect(findButton(compiled, 'Clear')).toBeUndefined();
      expect(buttons(compiled)).toHaveLength(1);
    });

    it('disables the placeholder button when disabled', async () => {
      const { compiled } = await setup({ disabled: true });

      expect(findButton(compiled, 'Not planned')?.disabled).toBe(true);
    });

    it('disables the placeholder button while saving', async () => {
      const { compiled } = await setup({ saving: true });

      expect(findButton(compiled, 'Not planned')?.disabled).toBe(true);
    });
  });

  describe('rendering an assigned slot', () => {
    it('shows the assigned guardian’s given name and formatted time when the guardian can be resolved', async () => {
      const { compiled } = await setup({
        guardians: [guardian('g1', 'Anna')],
        occurrence: occurrence({ kind: 0, guardianId: 'g1', time: '14:30:00' })
      });

      expect(compiled.textContent).toContain('Anna');
      expect(compiled.textContent).not.toContain('A guardian');
      expect(compiled.textContent).toContain('2:30 PM');
    });

    it('falls back to the generic "guardian" label when the assigned guardian id cannot be resolved', async () => {
      const { compiled } = await setup({
        guardians: [],
        occurrence: occurrence({ kind: 0, guardianId: 'missing-guardian' })
      });

      expect(compiled.textContent).toContain('A guardian');
    });

    it('shows the self-escort label and no time when none is set', async () => {
      const { compiled } = await setup({ occurrence: occurrence({ kind: 1 }) });

      expect(compiled.textContent).toContain('Goes alone');
      expect(compiled.querySelector('span.text-xs.text-slate-500')).toBeNull();
    });

    it('shows the assigned sibling’s given name when the sibling can be resolved', async () => {
      const { compiled } = await setup({
        siblings: [sibling('s1', 'Leo')],
        occurrence: occurrence({ kind: 2, siblingChildId: 's1' })
      });

      expect(compiled.textContent).toContain('Leo');
      expect(compiled.textContent).not.toContain('A sibling');
    });

    it('falls back to the generic "sibling" label when the assigned sibling id cannot be resolved', async () => {
      const { compiled } = await setup({
        siblings: [],
        occurrence: occurrence({ kind: 2, siblingChildId: 'missing-sibling' })
      });

      expect(compiled.textContent).toContain('A sibling');
    });

    it('shows the playdate host name', async () => {
      const { compiled } = await setup({
        occurrence: occurrence({ kind: 3, playdateHostName: 'Casper' })
      });

      expect(compiled.textContent).toContain('Casper');
    });

    it('shows a clear button that is enabled by default', async () => {
      const { compiled } = await setup({ occurrence: occurrence({ kind: 1 }) });

      expect(findButton(compiled, 'Clear')?.disabled).toBe(false);
    });

    it('hides the clear button and disables the summary button when disabled', async () => {
      const { compiled } = await setup({ disabled: true, occurrence: occurrence({ kind: 1 }) });

      expect(findButton(compiled, 'Clear')).toBeUndefined();
      expect(findButton(compiled, 'Goes alone')?.disabled).toBe(true);
    });

    it('keeps the clear button visible but disables both buttons while saving', async () => {
      const { compiled } = await setup({ saving: true, occurrence: occurrence({ kind: 1 }) });

      expect(findButton(compiled, 'Clear')?.disabled).toBe(true);
      expect(findButton(compiled, 'Goes alone')?.disabled).toBe(true);
    });
  });

  describe('clearing an assignment', () => {
    it('emits clear and does not open the edit form', async () => {
      const { compiled, onClear, onAssign } = await setup({ occurrence: occurrence({ kind: 1 }) });

      findButton(compiled, 'Clear')!.click();

      expect(onClear).toHaveBeenCalledTimes(1);
      expect(onAssign).not.toHaveBeenCalled();
    });
  });

  describe('opening the edit form', () => {
    it('defaults to the guardian kind with an empty selection and a disabled save button when starting from an unplanned slot', async () => {
      const { fixture, compiled } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      // SelectControlValueAccessor only finishes registering its [ngValue]-bound <option>s (and
      // writing the initial selection) after a further macrotask on a freshly-created @if branch --
      // see settle()/docs/testing.md's zoneless-async note.
      await settle(fixture);
      const [kindSelect] = selects(compiled);
      expect(kindSelect.selectedOptions[0].textContent?.trim()).toBe('A guardian');
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
    });

    it('does not open when disabled', async () => {
      const { fixture, compiled } = await setup({ disabled: true });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      expect(selects(compiled)).toHaveLength(0);
    });

    it('pre-fills every field from the existing occurrence for a playdate assignment', async () => {
      const existing = occurrence({
        kind: 3,
        playdateHostName: 'Casper',
        playdateLocation: 'The park',
        playdateContactInfo: '555-1234',
        time: '09:15:00',
        notes: 'Bring snacks'
      });
      const { fixture, compiled } = await setup({ occurrence: existing });

      findButton(compiled, 'Casper')!.click();
      await settle(fixture);

      const [kindSelect] = selects(compiled);
      expect(kindSelect.selectedOptions[0].textContent?.trim()).toBe('Playdate');

      const hostInput = compiled.querySelector<HTMLInputElement>('input[placeholder="Who’s hosting? (required)"]');
      const locationInput = compiled.querySelector<HTMLInputElement>('input[placeholder="Location (optional)"]');
      const contactInput = compiled.querySelector<HTMLInputElement>('input[placeholder="Contact info (optional)"]');
      const notesInput = compiled.querySelector<HTMLInputElement>('input[placeholder="Notes (optional)"]');

      expect(hostInput?.value).toBe('Casper');
      expect(locationInput?.value).toBe('The park');
      expect(contactInput?.value).toBe('555-1234');
      expect(notesInput?.value).toBe('Bring snacks');

      // TimeSelect renders "09:15" (stripped of seconds) in the native time input.
      expect(timeInput(compiled).value).toBe('09:15');
    });

    it('shows a "no siblings" hint when switching to the sibling kind with no siblings available', async () => {
      const { fixture, compiled } = await setup({ siblings: [] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      selectByLabel(selects(compiled)[0], 'A sibling');
      fixture.detectChanges();

      expect(compiled.textContent).toContain('No siblings linked yet.');
    });
  });

  describe('saving from the edit form', () => {
    it('emits a guardian assignment and closes the form on save', async () => {
      const { fixture, compiled, onAssign } = await setup({ guardians: [guardian('g1', 'Anna'), guardian('g2', 'Bob')] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      const [kindSelect, guardianSelect] = selects(compiled);
      selectByLabel(kindSelect, 'A guardian');
      fixture.detectChanges();
      selectByValue(guardianSelect, 'g1');
      fixture.detectChanges();

      expect(findButton(compiled, 'Save')?.disabled).toBe(false);
      findButton(compiled, 'Save')!.click();

      expect(onAssign).toHaveBeenCalledTimes(1);
      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request).toEqual({
        kind: 0,
        guardianId: 'g1',
        siblingChildId: null,
        playdateHostName: null,
        playdateLocation: null,
        playdateContactInfo: null,
        time: null,
        notes: null
      });

      fixture.detectChanges();
      expect(selects(compiled)).toHaveLength(0);
    });

    it('emits a sibling assignment with the chosen sibling id', async () => {
      const { fixture, compiled, onAssign } = await setup({ siblings: [sibling('s1', 'Leo')] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectByLabel(selects(compiled)[0], 'A sibling');
      fixture.detectChanges();
      selectByValue(selects(compiled)[1], 's1');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request.kind).toBe(2);
      expect(request.siblingChildId).toBe('s1');
      expect(request.guardianId).toBeNull();
    });

    it('allows saving a self-escort assignment immediately, with no id fields required', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectByLabel(selects(compiled)[0], 'Goes alone');
      fixture.detectChanges();

      expect(findButton(compiled, 'Save')?.disabled).toBe(false);
      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request).toEqual({
        kind: 1,
        guardianId: null,
        siblingChildId: null,
        playdateHostName: null,
        playdateLocation: null,
        playdateContactInfo: null,
        time: null,
        notes: null
      });
    });

    it('disables save for a playdate with only whitespace in the host name, and trims the saved fields', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectByLabel(selects(compiled)[0], 'Playdate');
      fixture.detectChanges();

      setInput(compiled, 'Who’s hosting? (required)', '   ');
      fixture.detectChanges();
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);

      setInput(compiled, 'Who’s hosting? (required)', '  Casper  ');
      setInput(compiled, 'Location (optional)', '   ');
      setInput(compiled, 'Contact info (optional)', '  555-1234  ');
      fixture.detectChanges();
      expect(findButton(compiled, 'Save')?.disabled).toBe(false);

      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request.kind).toBe(3);
      expect(request.playdateHostName).toBe('Casper');
      // Blank optional fields collapse to null rather than an empty/whitespace string.
      expect(request.playdateLocation).toBeNull();
      expect(request.playdateContactInfo).toBe('555-1234');
    });

    it('trims notes and sends null for a blank notes field', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectByLabel(selects(compiled)[0], 'Goes alone');
      setInput(compiled, 'Notes (optional)', '  needs a jacket  ');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();
      expect((onAssign.mock.calls[0][0] as AssignPickupRequest).notes).toBe('needs a jacket');
    });

    it('sends time as HH:mm:00 once a time is picked via the time input, using the last-selected kind', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectByLabel(selects(compiled)[0], 'Goes alone');
      fixture.detectChanges();

      setTime(compiled, '21:05');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();
      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request.time).toBe('21:05:00');
    });

    it('changing kind after picking a guardian resets which fields are shown, and canSave reflects the new kind', async () => {
      const { fixture, compiled } = await setup({ guardians: [guardian('g1', 'Anna')] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      const [kindSelect, guardianSelect] = selects(compiled);
      selectByLabel(kindSelect, 'A guardian');
      fixture.detectChanges();
      selectByValue(guardianSelect, 'g1');
      fixture.detectChanges();
      expect(findButton(compiled, 'Save')?.disabled).toBe(false);

      selectByLabel(selects(compiled)[0], 'A sibling');
      fixture.detectChanges();

      // The guardian select is gone (kind is now sibling) and no sibling has been chosen yet.
      expect(compiled.querySelector('select option[disabled]')).toBeTruthy();
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
    });
  });

  describe('cancelling the edit form', () => {
    it('closes the form without emitting and leaves the original assignment untouched', async () => {
      const existing = occurrence({ kind: 1 });
      const { fixture, compiled, onAssign, onClear } = await setup({ occurrence: existing });

      findButton(compiled, 'Goes alone')!.click();
      fixture.detectChanges();
      expect(selects(compiled)).not.toHaveLength(0);

      findButton(compiled, 'Cancel')!.click();
      fixture.detectChanges();

      expect(onAssign).not.toHaveBeenCalled();
      expect(onClear).not.toHaveBeenCalled();
      expect(selects(compiled)).toHaveLength(0);
      expect(compiled.textContent).toContain('Goes alone');
    });
  });
});
