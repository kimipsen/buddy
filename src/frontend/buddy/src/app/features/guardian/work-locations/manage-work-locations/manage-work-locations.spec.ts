import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  WorkLocation,
  WorkLocationSchedule,
  WorkLocationsService,
} from '../../../../core/work-locations.service';
import { ManageWorkLocations } from './manage-work-locations';

describe('ManageWorkLocations', () => {
  function location(overrides: Partial<WorkLocation> = {}): WorkLocation {
    return {
      id: 'loc-1',
      name: 'Stil',
      icon: '🏢',
      color: '#0ea5e9',
      isArchived: false,
      ...overrides,
    };
  }

  function schedule(locations: WorkLocation[]): WorkLocationSchedule {
    return {
      guardianId: 'me',
      locations,
      pattern: { cycleWeeks: 1, anchorMonday: '2026-09-28', days: [] },
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

  async function setup(locations: WorkLocation[], stub: Partial<WorkLocationsService> = {}) {
    const service: Partial<WorkLocationsService> = {
      addLocation: vi.fn(async () => location()),
      updateLocation: vi.fn(async () => location()),
      archiveLocation: vi.fn(async () => undefined),
      ...stub,
    };

    await TestBed.configureTestingModule({
      imports: [ManageWorkLocations],
      providers: [{ provide: WorkLocationsService, useValue: service }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ManageWorkLocations);
    fixture.componentRef.setInput('schedule', schedule(locations));
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, service, changed, compiled: fixture.nativeElement as HTMLElement };
  }

  it('shows the empty hint when there are no active locations', async () => {
    const { compiled } = await setup([location({ isArchived: true })]);

    expect(compiled.textContent).toContain('Add the places you work');
    expect(compiled.textContent).not.toContain('Stil');
  });

  it('adds a location with a trimmed name, the icon and the chosen color', async () => {
    const { fixture, compiled, service, changed } = await setup([]);
    const name = compiled.querySelector<HTMLInputElement>('#new-location-name')!;

    type(name, '  Randers  ');
    await settle(fixture);
    compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]')[0].click();
    await settle(fixture);
    findButton(compiled, 'Add location')!.click();
    await settle(fixture);

    expect(service.addLocation).toHaveBeenCalledWith({
      name: 'Randers',
      icon: '🏢',
      color: '#f43f5e',
    });
    expect(changed).toHaveBeenCalledOnce();
    expect(name.value).toBe('');
  });

  it('keeps Add disabled until a name is typed', async () => {
    const { compiled } = await setup([]);

    expect(findButton(compiled, 'Add location')!.disabled).toBe(true);
  });

  it('edits a location in place', async () => {
    const { fixture, compiled, service, changed } = await setup([location()]);

    findButton(compiled, 'Edit')!.click();
    await settle(fixture);
    type(compiled.querySelector<HTMLInputElement>('#edit-name-loc-1')!, 'Kontoret');
    await settle(fixture);
    findButton(compiled, 'Save')!.click();
    await settle(fixture);

    expect(service.updateLocation).toHaveBeenCalledWith('loc-1', {
      name: 'Kontoret',
      icon: '🏢',
      color: '#0ea5e9',
    });
    expect(changed).toHaveBeenCalledOnce();
    expect(compiled.querySelector('#edit-name-loc-1')).toBeNull();
  });

  it('cancels an edit without saving', async () => {
    const { fixture, compiled, service } = await setup([location()]);

    findButton(compiled, 'Edit')!.click();
    await settle(fixture);
    findButton(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(service.updateLocation).not.toHaveBeenCalled();
    expect(compiled.querySelector('#edit-name-loc-1')).toBeNull();
  });

  it('archives a location', async () => {
    const { fixture, compiled, service, changed } = await setup([location()]);

    findButton(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(service.archiveLocation).toHaveBeenCalledWith('loc-1');
    expect(changed).toHaveBeenCalledOnce();
  });

  it('shows the save error and does not report a change when the server rejects', async () => {
    const { fixture, compiled, changed } = await setup([], {
      addLocation: vi.fn(async () => {
        throw new Error('400');
      }),
    });

    type(compiled.querySelector<HTMLInputElement>('#new-location-name')!, 'Stil');
    await settle(fixture);
    findButton(compiled, 'Add location')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save the location. Names must be unique.');
    expect(changed).not.toHaveBeenCalled();
  });

  it('shows the archive error', async () => {
    const { fixture, compiled } = await setup([location()], {
      archiveLocation: vi.fn(async () => {
        throw new Error('500');
      }),
    });

    findButton(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to remove the location.');
  });
});
