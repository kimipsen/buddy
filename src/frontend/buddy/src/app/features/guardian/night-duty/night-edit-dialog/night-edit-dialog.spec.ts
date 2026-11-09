import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Night, NightEntry } from '../../../../core/night-duty.service';
import { NightPeople } from '../night-people';
import { NightEditDialog } from './night-edit-dialog';

describe('NightEditDialog', () => {
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
    babysitters: [{ guardianId: 'mom', id: 'maja', name: 'Maja', contactInfo: '' }],
  };

  const dad: NightEntry = { responsible: { kind: 0, guardianId: 'dad' }, note: 'Screen night' };

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 3; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function button(root: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
  }

  async function setup(night: Night, inputs: { busy?: boolean; error?: string | null } = {}) {
    await TestBed.configureTestingModule({ imports: [NightEditDialog] }).compileComponents();
    const fixture = TestBed.createComponent(NightEditDialog);
    fixture.componentRef.setInput('night', night);
    fixture.componentRef.setInput('dayLabel', 'Fri, Nov 13');
    fixture.componentRef.setInput('people', people);
    // Left unset unless a test needs them, so the inputs' defaults are what the dialog sees.
    if (inputs.busy !== undefined) {
      fixture.componentRef.setInput('busy', inputs.busy);
    }
    if (inputs.error !== undefined) {
      fixture.componentRef.setInput('error', inputs.error);
    }
    const save = vi.fn();
    const backToSchedule = vi.fn();
    const closed = vi.fn();
    fixture.componentInstance.save.subscribe(save);
    fixture.componentInstance.backToSchedule.subscribe(backToSchedule);
    fixture.componentInstance.closed.subscribe(closed);
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;
    return { fixture, compiled, save, backToSchedule, closed };
  }

  const fromSchedule: Night = {
    date: '2026-11-13',
    status: { kind: 2, entry: dad, source: 'Pattern', scheduled: null },
  };

  it('starts from the night and names what the schedule says', async () => {
    const { compiled } = await setup(fromSchedule);

    expect(compiled.querySelector('[role="dialog"]')?.getAttribute('aria-modal')).toBe('true');
    expect(compiled.textContent).toContain('Fri, Nov 13 night');
    expect(compiled.textContent).toContain('Fixed schedule: Dad · Screen night.');
    expect(compiled.querySelector<HTMLSelectElement>('#night-edit-who')?.value).toBe('g:dad');
    expect(compiled.querySelector<HTMLInputElement>('#night-edit-note')?.value).toBe(
      'Screen night',
    );
    // Not overridden, so there is nothing to go back to.
    expect(button(compiled, 'Back to schedule')).toBeUndefined();
  });

  it('keeps the schedule note when only the person changes', async () => {
    const { fixture, compiled, save } = await setup(fromSchedule);

    choose(compiled.querySelector('#night-edit-who')!, 'g:mom');
    await settle(fixture);
    button(compiled, 'Save night')!.click();

    expect(save).toHaveBeenCalledWith({
      responsible: { kind: 0, guardianId: 'mom' },
      note: 'Screen night',
    });
  });

  it('prefills an unplanned night with the schedule note and saves a sleepover', async () => {
    const { fixture, compiled, save } = await setup({
      date: '2026-11-14',
      status: { kind: 0 },
    });

    expect(compiled.textContent).toContain('The fixed schedule has nothing for this night.');
    choose(compiled.querySelector('#night-edit-who')!, 's');
    await settle(fixture);
    type(compiled.querySelector('#night-edit-host')!, ' Grandma Lis ');
    type(compiled.querySelector('#night-edit-location')!, 'Skovvej 12');
    await settle(fixture);
    button(compiled, 'Save night')!.click();

    expect(save).toHaveBeenCalledWith({
      responsible: {
        kind: 2,
        hostName: 'Grandma Lis',
        location: 'Skovvej 12',
        contactInfo: '',
      },
      note: '',
    });
  });

  it('keeps a stored sleepover contact it does not show', async () => {
    const { compiled, save } = await setup({
      date: '2026-11-14',
      status: {
        kind: 2,
        entry: {
          responsible: { kind: 2, hostName: 'Lis', location: 'Skovvej', contactInfo: '20 12' },
          note: '',
        },
        source: 'Override',
        scheduled: null,
      },
    });

    button(compiled, 'Save night')!.click();

    expect(save).toHaveBeenCalledWith({
      responsible: { kind: 2, hostName: 'Lis', location: 'Skovvej', contactInfo: '20 12' },
      note: '',
    });
  });

  it('asks for a host, a person or a note before saving', async () => {
    const { fixture, compiled, save } = await setup({ date: '2026-11-14', status: { kind: 0 } });

    choose(compiled.querySelector('#night-edit-who')!, '');
    await settle(fixture);
    button(compiled, 'Save night')!.click();
    await settle(fixture);

    expect(save).not.toHaveBeenCalled();
    expect(compiled.textContent).toContain('Choose who is on duty, write a note, or both.');
  });

  it('saves "nothing planned" as a cleared night and hides the note', async () => {
    const { fixture, compiled, save } = await setup(fromSchedule);

    choose(compiled.querySelector('#night-edit-who')!, 'cleared');
    await settle(fixture);

    expect(compiled.querySelector('#night-edit-note')).toBeNull();
    button(compiled, 'Save night')!.click();
    expect(save).toHaveBeenCalledWith(null);
  });

  it('starts a cleared night on "nothing planned" and offers going back to the schedule', async () => {
    const { compiled, backToSchedule } = await setup({
      date: '2026-11-13',
      status: { kind: 1, scheduled: dad },
    });

    expect(compiled.querySelector<HTMLSelectElement>('#night-edit-who')?.value).toBe('cleared');
    button(compiled, 'Back to schedule')!.click();
    expect(backToSchedule).toHaveBeenCalledOnce();
  });

  it('offers babysitters and closes on Cancel or Escape', async () => {
    const { compiled, closed } = await setup(fromSchedule);

    const options = Array.from(
      compiled.querySelectorAll<HTMLOptionElement>('#night-edit-who option'),
    ).map((o) => o.textContent?.trim());
    expect(options).toContain('🧑‍🍼 Maja');

    button(compiled, 'Cancel')!.click();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(closed).toHaveBeenCalledTimes(2);
  });

  it('shows the parent error and blocks while busy', async () => {
    const { compiled, save, closed } = await setup(fromSchedule, {
      busy: true,
      error: 'nightDuty.week.saveError',
    });

    expect(compiled.textContent).toContain('Unable to save the night.');
    expect(button(compiled, 'Save night')!.disabled).toBe(true);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    (compiled.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(save).not.toHaveBeenCalled();
    expect(closed).not.toHaveBeenCalled();
  });

  describe('more cases', () => {
    const mom: NightEntry = { responsible: { kind: 0, guardianId: 'mom' } };

    it('is labelled by its title and focuses the who select', async () => {
      const { compiled } = await setup(fromSchedule);
      const dialog = compiled.querySelector('[role="dialog"]')!;
      const labelledBy = dialog.getAttribute('aria-labelledby');

      expect(labelledBy).toBe('night-edit-title');
      expect(compiled.querySelector(`#${labelledBy}`)?.textContent).toContain('Fri, Nov 13 night');
      expect(document.activeElement).toBe(compiled.querySelector('#night-edit-who'));
    });

    it('keeps Tab inside the dialog', async () => {
      const { compiled } = await setup(fromSchedule);
      const saveButton = button(compiled, 'Save night')!;
      saveButton.focus();

      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', cancelable: true }));

      expect(document.activeElement).toBe(compiled.querySelector('#night-edit-who'));
    });

    it('names what the schedule says for a changed night, not the change itself', async () => {
      const { compiled } = await setup({
        date: '2026-11-13',
        status: { kind: 2, entry: mom, source: 'Override', scheduled: { ...dad, note: '' } },
      });

      expect(compiled.textContent).toContain('Fixed schedule: Dad. A change applies');
      expect(button(compiled, 'Back to schedule')).toBeDefined();
      // The override has no note, so the note starts empty.
      expect(compiled.querySelector<HTMLInputElement>('#night-edit-note')?.value).toBe('');
    });

    it('names an unknown guardian, an unnamed babysitter and a sleepover generically', async () => {
      const cases: [NightEntry, string][] = [
        [{ responsible: { kind: 0, guardianId: 'gone' } }, 'Fixed schedule: A guardian.'],
        [
          { responsible: { kind: 1, guardianId: 'mom', babysitterId: 'x', name: '' } },
          'Fixed schedule: Babysitter.',
        ],
        [{ responsible: { kind: 2, hostName: 'Lis' } }, 'Fixed schedule: Sleepover: Lis.'],
      ];

      for (const [scheduled, text] of cases) {
        const { compiled } = await setup({
          date: '2026-11-13',
          status: { kind: 1, scheduled },
        });
        expect(compiled.textContent).toContain(text);
        TestBed.resetTestingModule();
      }
    });

    it('starts an unplanned night on nobody, and a cleared night keeps an empty note when a person is chosen', async () => {
      const unplanned = await setup({ date: '2026-11-14', status: { kind: 0 } });
      expect(unplanned.compiled.querySelector<HTMLSelectElement>('#night-edit-who')?.value).toBe(
        '',
      );
      TestBed.resetTestingModule();

      const { fixture, compiled, save } = await setup({
        date: '2026-11-14',
        status: { kind: 1, scheduled: dad },
      });
      choose(compiled.querySelector('#night-edit-who')!, 'g:mom');
      await settle(fixture);
      button(compiled, 'Save night')!.click();

      expect(save).toHaveBeenCalledWith({ responsible: { kind: 0, guardianId: 'mom' }, note: '' });
    });

    it('starts a new sleepover with empty fields and trims the note', async () => {
      const { fixture, compiled, save } = await setup({ date: '2026-11-14', status: { kind: 0 } });

      choose(compiled.querySelector('#night-edit-who')!, 's');
      await settle(fixture);
      expect(compiled.querySelector<HTMLInputElement>('#night-edit-host')?.value).toBe('');
      expect(compiled.querySelector<HTMLInputElement>('#night-edit-location')?.value).toBe('');
      type(compiled.querySelector('#night-edit-host')!, 'Lis');
      type(compiled.querySelector('#night-edit-note')!, '  Pyjama party  ');
      await settle(fixture);
      button(compiled, 'Save night')!.click();

      expect(save).toHaveBeenCalledWith({
        responsible: { kind: 2, hostName: 'Lis', location: '', contactInfo: '' },
        note: 'Pyjama party',
      });
    });

    it('hides the "incomplete" message once the choice changes', async () => {
      const { fixture, compiled } = await setup({ date: '2026-11-14', status: { kind: 0 } });

      button(compiled, 'Save night')!.click();
      await settle(fixture);
      expect(compiled.textContent).toContain('Choose who is on duty');

      choose(compiled.querySelector('#night-edit-who')!, 'g:mom');
      await settle(fixture);
      expect(compiled.textContent).not.toContain('Choose who is on duty');
    });

    it('is not busy unless told so', async () => {
      const { compiled, save, closed } = await setup(fromSchedule);

      expect(button(compiled, 'Save night')!.disabled).toBe(false);
      button(compiled, 'Save night')!.click();
      button(compiled, 'Cancel')!.click();
      expect(save).toHaveBeenCalledOnce();
      expect(closed).toHaveBeenCalledOnce();
    });
  });
});
