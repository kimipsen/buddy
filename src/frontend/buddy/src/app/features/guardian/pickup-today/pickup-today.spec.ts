import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { PickupToday } from './pickup-today';

describe('PickupToday', () => {
  const today = todayIsoDate();

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Charlie', familyName: 'C' },
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
      date: today,
      slot: 0,
      kind: 1,
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

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    pickups?: Partial<PickupsService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      listChildGuardians: vi.fn(async () => []),
      ...stubs.guardians
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => []),
      ...stubs.pickups
    };

    await TestBed.configureTestingModule({
      imports: [PickupToday],
      providers: [provideRouter([]), { provide: GuardiansService, useValue: guardiansStub }, { provide: PickupsService, useValue: pickupsStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(PickupToday);

    return { fixture, guardians: guardiansStub, pickups: pickupsStub };
  }

  // loadToday chains more than one await (listMyChildren, then a Promise.all of per-child
  // listSchedule/listChildGuardians pairs, itself inside an outer Promise.all) before the signals
  // driving the template settle -- a single whenStable() flush isn't always enough, so flush a
  // generous fixed number of times rather than guessing when it's "probably" done.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  it('shows the loading spinner while the schedule is loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with no pickups today', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Nothing planned for today.');
    expect(compiled.querySelector('app-loading-spinner')).toBeFalsy();
  });

  it('shows the translated error message when loading the schedule fails', async () => {
    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s pickup schedule.');
  });

  it('shows a message pointing to Settings when the guardian has no linked children', async () => {
    const listSchedule = vi.fn(async () => []);
    const listChildGuardians = vi.fn(async () => []);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => []), listChildGuardians },
      pickups: { listSchedule }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Link a child from Settings to plan their pickups.');
    // With no children there is nothing to fetch a schedule or guardians for.
    expect(listSchedule).not.toHaveBeenCalled();
    expect(listChildGuardians).not.toHaveBeenCalled();
  });

  it('fetches the schedule for today only, scoped to each linked child', async () => {
    const listSchedule = vi.fn(async () => []);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1' })]) },
      pickups: { listSchedule }
    });
    await settle(fixture);

    expect(listSchedule).toHaveBeenCalledWith('child-1', today, today);
  });

  it('renders a self-escort pickup with its translated label', async () => {
    const selfEscort = occurrence({ slot: 1, kind: 1 });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [selfEscort]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pickup');
    expect(compiled.textContent).toContain('Goes alone');
  });

  it('renders a drop-off assigned to a sibling with its translated label', async () => {
    const sibling = occurrence({ slot: 0, kind: 2, siblingChildId: 'sibling-1' });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [sibling]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Drop-off');
    expect(compiled.textContent).toContain('A sibling');
  });

  it('renders a playdate pickup with the host name, untranslated', async () => {
    const playdate = occurrence({ kind: 3, playdateHostName: 'The Andersens' });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [playdate]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('The Andersens');
  });

  it('resolves a guardian assignee to their given name using that child’s guardian list', async () => {
    const assignedToGina = occurrence({ kind: 0, guardianId: 'guardian-1' });

    const { fixture } = await setup({
      guardians: { listChildGuardians: vi.fn(async () => [guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'Guardian' } })]) },
      pickups: { listSchedule: vi.fn(async () => [assignedToGina]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    // Only the given name is shown, not the family name.
    expect(compiled.textContent).toContain('Gina');
    expect(compiled.textContent).not.toContain('Guardian');
  });

  it('falls back to a generic "guardian" label when the assigned guardian id cannot be resolved', async () => {
    const assignedToUnknown = occurrence({ kind: 0, guardianId: 'missing-guardian' });

    const { fixture } = await setup({
      guardians: { listChildGuardians: vi.fn(async () => [guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' } })]) },
      pickups: { listSchedule: vi.fn(async () => [assignedToUnknown]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('A guardian');
    expect(compiled.textContent).not.toContain('Gina');
  });

  it('does not show the child name when the guardian has only one linked child', async () => {
    const dropOff = occurrence({ slot: 0, kind: 1 });

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } })]) },
      pickups: { listSchedule: vi.fn(async () => [dropOff]) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Charlie');
  });

  it('labels each row with its child’s name when the guardian has multiple children, and sorts drop-off before pickup', async () => {
    const charlie = child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } });
    const dana = child({ id: 'child-2', name: { givenName: 'Dana', familyName: 'D' } });

    const listSchedule = vi.fn(async (childId: string) =>
      childId === 'child-1' ? [occurrence({ slot: 1, kind: 1 })] : [occurrence({ slot: 0, kind: 1 })]
    );

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [charlie, dana]) },
      pickups: { listSchedule }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Charlie');
    expect(compiled.textContent).toContain('Dana');

    const rows = Array.from(compiled.querySelectorAll('li')).map((li) => li.textContent ?? '');
    expect(rows).toHaveLength(2);
    // Dana's drop-off (slot 0) sorts ahead of Charlie's pickup (slot 1), across children.
    expect(rows[0]).toContain('Dana');
    expect(rows[0]).toContain('Drop-off');
    expect(rows[1]).toContain('Charlie');
    expect(rows[1]).toContain('Pickup');
  });

  it('keeps each child’s guardian assignees resolved against that same child’s guardian list, not another child’s', async () => {
    const charlie = child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } });
    const dana = child({ id: 'child-2', name: { givenName: 'Dana', familyName: 'D' } });

    const listSchedule = vi.fn(async (childId: string) =>
      childId === 'child-1' ? [occurrence({ slot: 0, kind: 0, guardianId: 'guardian-1' })] : [occurrence({ slot: 1, kind: 0, guardianId: 'guardian-1' })]
    );
    const listChildGuardians = vi.fn(async (childId: string) =>
      childId === 'child-1'
        ? [guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' } })]
        : [guardian({ id: 'guardian-1', name: { givenName: 'Peter', familyName: 'P' } })]
    );

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [charlie, dana]), listChildGuardians },
      pickups: { listSchedule }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const rows = Array.from(compiled.querySelectorAll('li')).map((li) => li.textContent ?? '');
    expect(rows.find((text) => text.includes('Charlie'))).toContain('Gina');
    expect(rows.find((text) => text.includes('Dana'))).toContain('Peter');
  });
});
