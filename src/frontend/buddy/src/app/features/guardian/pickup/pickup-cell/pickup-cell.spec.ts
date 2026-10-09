import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { ChildBabysitter } from '../../../../core/babysitters.service';
import { ChildSummary, GuardianSummary } from '../../../../core/guardians.service';
import { AssignPickupRequest, PickupOccurrence } from '../../../../core/pickups.service';
import { PickupCell } from './pickup-cell';
import { disableFeatures } from '../../../../../testing/features-fixture';

describe('PickupCell', () => {
  function guardian(id: string, givenName: string): GuardianSummary {
    return {
      id,
      name: { givenName, familyName: 'Guardian' },
      guardianLinkId: `link-${id}`,
      kind: 'Guardian',
    };
  }

  function sibling(id: string, givenName: string): ChildSummary {
    return {
      id,
      name: { givenName, familyName: 'Kid' },
      guardianLinkId: `link-${id}`,
      kind: 'Parent',
      language: 'en',
      timeZoneId: 'UTC',
    };
  }

  function occurrence(overrides: Partial<PickupOccurrence> = {}): PickupOccurrence {
    return {
      assignee: { kind: 0, guardianId: 'guardian-1' },
      date: '2026-08-26',
      slot: 'DropOff',
      time: null,
      notes: '',
      assignedBy: 'guardian-1',
      ...overrides,
    };
  }

  function babysitter(guardianId: string, id: string, name: string): ChildBabysitter {
    return { guardianId, id, name, contactInfo: '' };
  }

  interface Options {
    babysitters?: ChildBabysitter[];
    guardians?: GuardianSummary[];
    siblings?: ChildSummary[];
    occurrence?: PickupOccurrence | null;
    disabled?: boolean;
    saving?: boolean;
  }

  async function setup(options: Options = {}) {
    await TestBed.configureTestingModule({
      imports: [PickupCell],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(PickupCell);
    const onAssign = vi.fn();
    const onClear = vi.fn();
    fixture.componentInstance.assign.subscribe(onAssign);
    fixture.componentInstance.clear.subscribe(onClear);

    fixture.componentRef.setInput('guardians', options.guardians ?? []);
    fixture.componentRef.setInput('siblings', options.siblings ?? []);
    if (options.babysitters !== undefined) {
      fixture.componentRef.setInput('babysitters', options.babysitters);
    }
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
  // is needed before reading selectedOptions on a just-opened edit form. This still applies to the
  // guardian/sibling `<select>` (the "kind" picker itself is a segmented-control radiogroup, not a
  // native select, so it settles synchronously).
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

  // The "kind" picker (guardian/self-escort/sibling/playdate) is an `app-segmented-control`: a
  // `role="radiogroup"` of `role="radio"` buttons, not a native `<select>` -- see
  // shared/segmented-control/segmented-control.html.
  function kindRadios(compiled: HTMLElement): HTMLButtonElement[] {
    return Array.from(
      compiled.querySelectorAll<HTMLButtonElement>('[role="radiogroup"] button[role="radio"]'),
    );
  }

  function selectKind(compiled: HTMLElement, label: string): void {
    const button = kindRadios(compiled).find(
      (candidate) => candidate.textContent?.trim() === label,
    );
    expect(button, `kind option "${label}" not found`).toBeTruthy();
    button!.click();
  }

  function selectedKindLabel(compiled: HTMLElement): string | undefined {
    return kindRadios(compiled)
      .find((button) => button.getAttribute('aria-checked') === 'true')
      ?.textContent?.trim();
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
        occurrence: occurrence({ assignee: { kind: 0, guardianId: 'g1' }, time: '14:30:00' }),
      });

      expect(compiled.textContent).toContain('Anna');
      expect(compiled.textContent).not.toContain('A guardian');
      expect(compiled.textContent).toContain('2:30 PM');
    });

    it('falls back to the generic "guardian" label when the assigned guardian id cannot be resolved', async () => {
      const { compiled } = await setup({
        guardians: [],
        occurrence: occurrence({ assignee: { kind: 0, guardianId: 'missing-guardian' } }),
      });

      expect(compiled.textContent).toContain('A guardian');
    });

    it('shows the self-escort label and no time when none is set', async () => {
      const { compiled } = await setup({ occurrence: occurrence({ assignee: { kind: 1 } }) });

      expect(compiled.textContent).toContain('Goes alone');
      expect(compiled.querySelector('span.text-xs.text-slate-500')).toBeNull();
    });

    it('shows the assigned sibling’s given name when the sibling can be resolved', async () => {
      const { compiled } = await setup({
        siblings: [sibling('s1', 'Leo')],
        occurrence: occurrence({ assignee: { kind: 2, siblingChildId: 's1' } }),
      });

      expect(compiled.textContent).toContain('Leo');
      expect(compiled.textContent).not.toContain('A sibling');
    });

    it('falls back to the generic "sibling" label when the assigned sibling id cannot be resolved', async () => {
      const { compiled } = await setup({
        siblings: [],
        occurrence: occurrence({ assignee: { kind: 2, siblingChildId: 'missing-sibling' } }),
      });

      expect(compiled.textContent).toContain('A sibling');
    });

    it('shows the playdate host name', async () => {
      const { compiled } = await setup({
        occurrence: occurrence({
          assignee: { kind: 3, hostName: 'Casper', location: '', contactInfo: '' },
        }),
      });

      expect(compiled.textContent).toContain('Casper');
    });

    it('shows the babysitter’s resolved name', async () => {
      const { compiled } = await setup({
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: 'Anna' },
        }),
      });

      expect(compiled.textContent).toContain('Anna');
    });

    it('falls back to the generic "babysitter" label when the name no longer resolves', async () => {
      const { compiled } = await setup({
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: '' },
        }),
      });

      expect(findButton(compiled, 'Babysitter')).toBeTruthy();
    });

    it('shows a clear button that is enabled by default', async () => {
      const { compiled } = await setup({ occurrence: occurrence({ assignee: { kind: 1 } }) });

      expect(findButton(compiled, 'Clear')?.disabled).toBe(false);
    });

    it('hides the clear button and disables the summary button when disabled', async () => {
      const { compiled } = await setup({
        disabled: true,
        occurrence: occurrence({ assignee: { kind: 1 } }),
      });

      expect(findButton(compiled, 'Clear')).toBeUndefined();
      expect(findButton(compiled, 'Goes alone')?.disabled).toBe(true);
    });

    it('keeps the clear button visible but disables both buttons while saving', async () => {
      const { compiled } = await setup({
        saving: true,
        occurrence: occurrence({ assignee: { kind: 1 } }),
      });

      expect(findButton(compiled, 'Clear')?.disabled).toBe(true);
      expect(findButton(compiled, 'Goes alone')?.disabled).toBe(true);
    });
  });

  describe('clearing an assignment', () => {
    it('emits clear and does not open the edit form', async () => {
      const { compiled, onClear, onAssign } = await setup({
        occurrence: occurrence({ assignee: { kind: 1 } }),
      });

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
      expect(selectedKindLabel(compiled)).toBe('A guardian');
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
    });

    // jsdom has no layout, so this checks the classes: the form never gets wider than a phone, and
    // scrolls rather than running off a short screen.
    it('keeps the edit form within a phone screen', async () => {
      const { fixture, compiled } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      const form = compiled.querySelector('[role="radiogroup"]')!.closest('.shadow-lg')!;
      expect(form.classList).toContain('max-w-[calc(100vw-2rem)]');
      expect(form.classList).toContain('max-h-[calc(100dvh-5rem)]');
      expect(form.classList).toContain('overflow-y-auto');
    });

    it('does not open when disabled', async () => {
      const { fixture, compiled } = await setup({ disabled: true });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      expect(selects(compiled)).toHaveLength(0);
    });

    it('pre-fills every field from the existing occurrence for a playdate assignment', async () => {
      const existing = occurrence({
        assignee: { kind: 3, hostName: 'Casper', location: 'The park', contactInfo: '555-1234' },
        time: '09:15:00',
        notes: 'Bring snacks',
      });
      const { fixture, compiled } = await setup({ occurrence: existing });

      findButton(compiled, 'Casper')!.click();
      await settle(fixture);

      expect(selectedKindLabel(compiled)).toBe('Playdate');

      const hostInput = compiled.querySelector<HTMLInputElement>(
        'input[placeholder="Who’s hosting? (required)"]',
      );
      const locationInput = compiled.querySelector<HTMLInputElement>(
        'input[placeholder="Location (optional)"]',
      );
      const contactInput = compiled.querySelector<HTMLInputElement>(
        'input[placeholder="Contact info (optional)"]',
      );
      const notesInput = compiled.querySelector<HTMLInputElement>(
        'input[placeholder="Notes (optional)"]',
      );

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

      selectKind(compiled, 'A sibling');
      fixture.detectChanges();

      expect(compiled.textContent).toContain('No siblings linked yet.');
    });
  });

  describe('picking a babysitter', () => {
    it('shows a "no babysitters" hint with a link to manage them when none are saved', async () => {
      const { fixture, compiled } = await setup({ babysitters: [] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Babysitter');
      fixture.detectChanges();

      expect(compiled.textContent).toContain('No babysitters saved yet.');
      const link = compiled.querySelector<HTMLAnchorElement>('a[href="/guardian/babysitters"]');
      expect(link?.textContent?.trim()).toBe('Manage babysitters');
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
    });

    it('emits the chosen babysitter with its owning guardian', async () => {
      const { fixture, compiled, onAssign } = await setup({
        babysitters: [babysitter('g1', 'b1', 'Anna'), babysitter('g2', 'b2', 'Bo')],
      });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Babysitter');
      fixture.detectChanges();
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
      selectByValue(selects(compiled)[0], 'g2|b2');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request.assignee).toEqual({
        kind: 4,
        guardianId: 'g2',
        babysitterId: 'b2',
        name: 'Bo',
      });
    });

    it('pre-selects the assigned babysitter when editing', async () => {
      const { fixture, compiled } = await setup({
        babysitters: [babysitter('g1', 'b1', 'Anna')],
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: 'Anna' },
        }),
      });

      findButton(compiled, 'Anna')!.click();
      await settle(fixture);

      expect(selectedKindLabel(compiled)).toBe('Babysitter');
      expect(selects(compiled)[0].value).toBe('g1|b1');
      expect(findButton(compiled, 'Save')?.disabled).toBe(false);
    });

    it('cannot re-save a babysitter who has left the list (archived)', async () => {
      const { fixture, compiled } = await setup({
        babysitters: [],
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: 'Anna' },
        }),
      });

      findButton(compiled, 'Anna')!.click();
      await settle(fixture);

      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
      expect(compiled.textContent).toContain('This babysitter was removed. Choose someone else.');
    });
  });

  describe('saving from the edit form', () => {
    it('emits a guardian assignment and closes the form on save', async () => {
      const { fixture, compiled, onAssign } = await setup({
        guardians: [guardian('g1', 'Anna'), guardian('g2', 'Bob')],
      });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      selectKind(compiled, 'A guardian');
      fixture.detectChanges();
      const [guardianSelect] = selects(compiled);
      selectByValue(guardianSelect, 'g1');
      fixture.detectChanges();

      expect(findButton(compiled, 'Save')?.disabled).toBe(false);
      findButton(compiled, 'Save')!.click();

      expect(onAssign).toHaveBeenCalledTimes(1);
      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request).toEqual({ assignee: { kind: 0, guardianId: 'g1' }, time: null, notes: '' });

      fixture.detectChanges();
      expect(selects(compiled)).toHaveLength(0);
    });

    it('emits a sibling assignment with the chosen sibling id', async () => {
      const { fixture, compiled, onAssign } = await setup({ siblings: [sibling('s1', 'Leo')] });

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'A sibling');
      fixture.detectChanges();
      selectByValue(selects(compiled)[0], 's1');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      // Only the sibling case's own field is sent -- no stray guardianId from the form.
      expect(request.assignee).toEqual({ kind: 2, siblingChildId: 's1' });
    });

    it('allows saving a self-escort assignment immediately, with no id fields required', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Goes alone');
      fixture.detectChanges();

      expect(findButton(compiled, 'Save')?.disabled).toBe(false);
      findButton(compiled, 'Save')!.click();

      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request).toEqual({ assignee: { kind: 1 }, time: null, notes: '' });
    });

    it('disables save for a playdate with only whitespace in the host name, and trims the saved fields', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Playdate');
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
      // Text is trimmed, and a blank optional field is sent as '' ("not given").
      expect(request.assignee).toEqual({
        kind: 3,
        hostName: 'Casper',
        location: '',
        contactInfo: '555-1234',
      });
    });

    it('trims notes and sends null for a blank notes field', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Goes alone');
      setInput(compiled, 'Notes (optional)', '  needs a jacket  ');
      fixture.detectChanges();

      findButton(compiled, 'Save')!.click();
      expect((onAssign.mock.calls[0][0] as AssignPickupRequest).notes).toBe('needs a jacket');
    });

    it('sends time as HH:mm:00 once a time is picked via the time input, using the last-selected kind', async () => {
      const { fixture, compiled, onAssign } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();
      selectKind(compiled, 'Goes alone');
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

      selectKind(compiled, 'A guardian');
      fixture.detectChanges();
      const [guardianSelect] = selects(compiled);
      selectByValue(guardianSelect, 'g1');
      fixture.detectChanges();
      expect(findButton(compiled, 'Save')?.disabled).toBe(false);

      selectKind(compiled, 'A sibling');
      fixture.detectChanges();

      // The guardian select is gone (kind is now sibling) and no sibling has been chosen yet.
      expect(compiled.querySelector('select option[disabled]')).toBeTruthy();
      expect(findButton(compiled, 'Save')?.disabled).toBe(true);
    });
  });

  describe('cancelling the edit form', () => {
    it('closes the form without emitting and leaves the original assignment untouched', async () => {
      const existing = occurrence({ assignee: { kind: 1 } });
      const { fixture, compiled, onAssign, onClear } = await setup({ occurrence: existing });

      findButton(compiled, 'Goes alone')!.click();
      fixture.detectChanges();
      expect(compiled.querySelector('[role="radiogroup"]')).toBeTruthy();

      findButton(compiled, 'Cancel')!.click();
      fixture.detectChanges();

      expect(onAssign).not.toHaveBeenCalled();
      expect(onClear).not.toHaveBeenCalled();
      expect(compiled.querySelector('[role="radiogroup"]')).toBeNull();
      expect(compiled.textContent).toContain('Goes alone');
    });
  });

  describe('with babysitters turned off', () => {
    it('offers no babysitter option for a new assignment', async () => {
      disableFeatures('babysitters');
      const { fixture, compiled } = await setup();

      findButton(compiled, 'Not planned')!.click();
      fixture.detectChanges();

      const labels = kindRadios(compiled).map((radio) => radio.textContent?.trim());
      expect(labels).toHaveLength(4);
      expect(labels).not.toContain('Babysitter');
    });

    it('keeps the option, without the link to manage babysitters, for a slot already assigned to one', async () => {
      disableFeatures('babysitters');
      const { fixture, compiled } = await setup({
        babysitters: [],
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: 'Anna' },
        }),
      });

      findButton(compiled, 'Anna')!.click();
      fixture.detectChanges();

      expect(selectedKindLabel(compiled)).toBe('Babysitter');
      expect(compiled.querySelector('a[href="/guardian/babysitters"]')).toBeNull();
    });

    it('re-saves a slot already assigned to a babysitter without calling them removed', async () => {
      disableFeatures('babysitters');
      const { fixture, compiled, onAssign } = await setup({
        babysitters: [],
        occurrence: occurrence({
          assignee: { kind: 4, guardianId: 'g1', babysitterId: 'b1', name: 'Anna' },
        }),
      });

      findButton(compiled, 'Anna')!.click();
      fixture.detectChanges();

      const options = Array.from(selects(compiled)[0].options).map((o) => o.textContent?.trim());
      expect(options).toContain('Anna');
      expect(compiled.textContent).not.toContain('This babysitter was removed');
      expect(findButton(compiled, 'Save')?.disabled).toBe(false);

      findButton(compiled, 'Save')!.click();

      expect(onAssign).toHaveBeenCalledTimes(1);
      const request: AssignPickupRequest = onAssign.mock.calls[0][0];
      expect(request.assignee).toEqual({
        kind: 4,
        guardianId: 'g1',
        babysitterId: 'b1',
        name: 'Anna',
      });
    });
  });
});
