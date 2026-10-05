import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { Babysitter, BabysittersService } from '../../../core/babysitters.service';
import { GuardianBabysitters } from './babysitters';

describe('GuardianBabysitters', () => {
  function babysitter(overrides: Partial<Babysitter> = {}): Babysitter {
    return {
      id: 'b-1',
      name: 'Anna',
      contactInfo: '+45 12 34 56 78',
      isArchived: false,
      ...overrides,
    };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    for (let i = 0; i < 3; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function findButton(root: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function setup(stub: Partial<BabysittersService> = {}) {
    const service: Partial<BabysittersService> = {
      listMine: vi.fn(async () => [
        babysitter(),
        babysitter({ id: 'b-2', name: 'Old', isArchived: true }),
      ]),
      add: vi.fn(async () => babysitter()),
      update: vi.fn(async () => babysitter()),
      archive: vi.fn(async () => undefined),
      ...stub,
    };

    await TestBed.configureTestingModule({
      imports: [GuardianBabysitters],
      providers: [provideRouter([]), { provide: BabysittersService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianBabysitters);
    await settle(fixture);

    return { fixture, service, compiled: fixture.nativeElement as HTMLElement };
  }

  it('lists active babysitters with their contact info and hides archived ones', async () => {
    const { compiled } = await setup();

    expect(compiled.textContent).toContain('Anna');
    expect(compiled.textContent).toContain('+45 12 34 56 78');
    expect(compiled.textContent).not.toContain('Old');
  });

  it('names the babysitter in each row action’s accessible label', async () => {
    const { compiled } = await setup();

    expect(findButton(compiled, 'Edit')!.getAttribute('aria-label')).toBe('Edit Anna');
    expect(findButton(compiled, 'Remove')!.getAttribute('aria-label')).toBe('Remove Anna');
  });

  it('shows the empty hint when there are no active babysitters', async () => {
    const { compiled } = await setup({
      listMine: vi.fn(async () => [babysitter({ isArchived: true })]),
    });

    expect(compiled.textContent).toContain('No babysitters yet');
  });

  it('shows an error when the list cannot load', async () => {
    const { compiled } = await setup({
      listMine: vi.fn(async () => {
        throw new Error('boom');
      }),
    });

    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'Unable to load your babysitters.',
    );
  });

  it('adds a babysitter with trimmed details, then reloads and clears the form', async () => {
    const { fixture, compiled, service } = await setup();
    const name = compiled.querySelector<HTMLInputElement>('#new-babysitter-name')!;
    const contact = compiled.querySelector<HTMLInputElement>('#new-babysitter-contact')!;
    const addButton = findButton(compiled, 'Add babysitter')!;

    expect(addButton.disabled).toBe(true);
    type(name, '  Bo  ');
    type(contact, ' bo@example.com ');
    await settle(fixture);
    expect(addButton.disabled).toBe(false);

    addButton.click();
    await settle(fixture);

    expect(service.add).toHaveBeenCalledWith({ name: 'Bo', contactInfo: 'bo@example.com' });
    expect(service.listMine).toHaveBeenCalledTimes(2);
    expect(name.value).toBe('');
    expect(contact.value).toBe('');
  });

  it('edits a babysitter in place', async () => {
    const { fixture, compiled, service } = await setup();

    findButton(compiled, 'Edit')!.click();
    await settle(fixture);
    const name = compiled.querySelector<HTMLInputElement>('#edit-name-b-1')!;
    expect(name.value).toBe('Anna');
    type(name, 'Anne ');
    type(compiled.querySelector<HTMLInputElement>('#edit-contact-b-1')!, '');
    await settle(fixture);

    findButton(compiled, 'Save')!.click();
    await settle(fixture);

    expect(service.update).toHaveBeenCalledWith('b-1', { name: 'Anne', contactInfo: '' });
    expect(compiled.querySelector('#edit-name-b-1')).toBeNull();
  });

  it('cancels an edit without saving', async () => {
    const { fixture, compiled, service } = await setup();

    findButton(compiled, 'Edit')!.click();
    await settle(fixture);
    findButton(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(service.update).not.toHaveBeenCalled();
    expect(compiled.querySelector('#edit-name-b-1')).toBeNull();
  });

  it('removes (archives) a babysitter', async () => {
    const { fixture, compiled, service } = await setup();

    findButton(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(service.archive).toHaveBeenCalledWith('b-1');
    expect(service.listMine).toHaveBeenCalledTimes(2);
  });

  it('shows the save error when adding fails', async () => {
    const { fixture, compiled } = await setup({
      add: vi.fn(async () => {
        throw new Error('boom');
      }),
    });
    type(compiled.querySelector<HTMLInputElement>('#new-babysitter-name')!, 'Bo');
    await settle(fixture);

    findButton(compiled, 'Add babysitter')!.click();
    await settle(fixture);

    expect(compiled.querySelector('[role="alert"]')?.textContent).toContain(
      'Unable to save the babysitter.',
    );
  });
});
