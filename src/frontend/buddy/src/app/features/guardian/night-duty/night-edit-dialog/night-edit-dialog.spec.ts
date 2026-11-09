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
    fixture.componentRef.setInput('busy', inputs.busy ?? false);
    fixture.componentRef.setInput('error', inputs.error ?? null);
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
});
