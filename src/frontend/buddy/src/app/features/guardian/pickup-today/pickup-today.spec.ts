import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { todayIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { PickupOccurrence, PickupsService } from '../../../core/pickups.service';
import { PER_ITEM_REQUEST_CONCURRENCY } from '../../../core/map-with-concurrency';
import { PickupToday } from './pickup-today';

describe('PickupToday', () => {
  const today = todayIsoDate();

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Charlie', familyName: 'C' },
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
      assignee: { kind: 1 },
      date: today,
      slot: 'DropOff',
      time: null,
      notes: '',
      assignedBy: 'guardian-1',
      ...overrides,
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
      ...stubs.guardians,
    };
    const pickupsStub: Partial<PickupsService> = {
      listSchedule: vi.fn(async () => []),
      ...stubs.pickups,
    };

    await TestBed.configureTestingModule({
      imports: [PickupToday],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: PickupsService, useValue: pickupsStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(PickupToday);

    return { fixture, guardians: guardiansStub, pickups: pickupsStub };
  }

  // loadToday chains more than one await (listMyChildren, then a mapWithConcurrency over per-child
  // listSchedule/listChildGuardians Promise.all pairs) before the resource driving the template
  // settles. whenStable() would wait for the resource's pending load, which never finishes while a
  // test holds a request open, so a macrotask flush is used instead, repeated to cover any depth of
  // chained awaits.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
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
    const { fixture } = await setup({
      pickups: { listSchedule: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load today’s pickup schedule.');
  });

  it('shows a message pointing to Settings when the guardian has no linked children', async () => {
    const listSchedule = vi.fn(async () => []);
    const listChildGuardians = vi.fn(async () => []);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => []), listChildGuardians },
      pickups: { listSchedule },
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
      pickups: { listSchedule },
    });
    await settle(fixture);

    expect(listSchedule).toHaveBeenCalledWith('child-1', today, today);
  });

  it('renders a self-escort pickup with its translated label', async () => {
    const selfEscort = occurrence({ assignee: { kind: 1 }, slot: 'PickUp' });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [selfEscort]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Pickup');
    expect(compiled.textContent).toContain('Goes alone');
  });

  it('renders a drop-off assigned to a sibling with its translated label', async () => {
    const sibling = occurrence({
      assignee: { kind: 2, siblingChildId: 'sibling-1' },
      slot: 'DropOff',
    });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [sibling]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Drop-off');
    expect(compiled.textContent).toContain('A sibling');
  });

  it('renders a playdate pickup with the host name, untranslated', async () => {
    const playdate = occurrence({
      assignee: { kind: 3, hostName: 'The Andersens', location: '', contactInfo: '' },
    });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [playdate]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('The Andersens');
  });

  it('renders a babysitter pickup with the resolved name, or the generic label without one', async () => {
    const named = occurrence({
      assignee: { kind: 4, guardianId: 'guardian-1', babysitterId: 'b1', name: 'Anna' },
    });
    const unnamed = occurrence({
      slot: 'PickUp',
      assignee: { kind: 4, guardianId: 'guardian-1', babysitterId: 'b2', name: '' },
    });

    const { fixture } = await setup({
      pickups: { listSchedule: vi.fn(async () => [named, unnamed]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Anna');
    expect(compiled.textContent).toContain('Babysitter');
  });

  it('resolves a guardian assignee to their given name using that child’s guardian list', async () => {
    const assignedToGina = occurrence({ assignee: { kind: 0, guardianId: 'guardian-1' } });

    const { fixture } = await setup({
      guardians: {
        listChildGuardians: vi.fn(async () => [
          guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'Guardian' } }),
        ]),
      },
      pickups: { listSchedule: vi.fn(async () => [assignedToGina]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    // Only the given name is shown, not the family name.
    expect(compiled.textContent).toContain('Gina');
    expect(compiled.textContent).not.toContain('Guardian');
  });

  it('falls back to a generic "guardian" label when the assigned guardian id cannot be resolved', async () => {
    const assignedToUnknown = occurrence({ assignee: { kind: 0, guardianId: 'missing-guardian' } });

    const { fixture } = await setup({
      guardians: {
        listChildGuardians: vi.fn(async () => [
          guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' } }),
        ]),
      },
      pickups: { listSchedule: vi.fn(async () => [assignedToUnknown]) },
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('A guardian');
    expect(compiled.textContent).not.toContain('Gina');
  });

  // Each body row as [child, drop-off, pickup] cell texts.
  function tableRows(fixture: { nativeElement: unknown }): string[][] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')).map(
      (tr) => Array.from(tr.children).map((cell) => cell.textContent?.trim() ?? ''),
    );
  }

  it('renders a table with drop-off and pickup column headers, drop-off first', async () => {
    const { fixture } = await setup({
      pickups: { listSchedule: vi.fn(async () => [occurrence({ slot: 'DropOff' })]) },
    });
    await settle(fixture);

    const headers = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('thead th[scope="col"]'),
    ).map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Child', 'Drop-off', 'Pickup']);
  });

  it('shows the child as a row header even when the guardian has only one linked child', async () => {
    const { fixture } = await setup({
      pickups: { listSchedule: vi.fn(async () => [occurrence({ slot: 'DropOff' })]) },
    });
    await settle(fixture);

    const rowHeader = (fixture.nativeElement as HTMLElement).querySelector('tbody th[scope="row"]');
    expect(rowHeader?.textContent?.trim()).toBe('Charlie');
  });

  it('puts each slot in its own column and marks an unplanned slot as not planned', async () => {
    const pickupOnly = occurrence({ assignee: { kind: 1 }, slot: 'PickUp' });

    const { fixture } = await setup({ pickups: { listSchedule: vi.fn(async () => [pickupOnly]) } });
    await settle(fixture);

    const [row] = tableRows(fixture);
    expect(row[0]).toBe('Charlie');
    expect(row[1]).toContain('Not planned');
    expect(row[2]).toContain('Goes alone');
    expect(row[2]).not.toContain('Not planned');
  });

  it('shows one row per child, sorted by child name, with drop-off and pickup side by side', async () => {
    const charlie = child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } });
    const dana = child({ id: 'child-2', name: { givenName: 'Dana', familyName: 'D' } });

    // Pickup returned before drop-off, and Dana listed before Charlie, so neither input order
    // matches the expected output.
    const listSchedule = vi.fn(async (childId: string) => [
      occurrence({ assignee: { kind: 1 }, slot: 'PickUp' }),
      occurrence({
        assignee: childId === 'child-1' ? { kind: 2, siblingChildId: 'sibling-1' } : { kind: 1 },
        slot: 'DropOff',
      }),
    ]);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [dana, charlie]) },
      pickups: { listSchedule },
    });
    await settle(fixture);

    const rows = tableRows(fixture);
    expect(rows.map((row) => row[0])).toEqual(['Charlie', 'Dana']);
    expect(rows[0][1]).toContain('A sibling');
    expect(rows[0][2]).toContain('Goes alone');
    expect(rows[1][1]).toContain('Goes alone');
  });

  it('orders same-named children by id so the order is stable', async () => {
    const first = child({ id: 'child-a', name: { givenName: 'Sam', familyName: 'A' } });
    const second = child({ id: 'child-b', name: { givenName: 'Sam', familyName: 'B' } });

    const listSchedule = vi.fn(async (childId: string) => [
      occurrence({
        assignee: childId === 'child-a' ? { kind: 1 } : { kind: 2, siblingChildId: 'sibling-1' },
        slot: 'DropOff',
      }),
    ]);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [second, first]) },
      pickups: { listSchedule },
    });
    await settle(fixture);

    const rows = tableRows(fixture);
    expect(rows[0][1]).toContain('Goes alone');
    expect(rows[1][1]).toContain('A sibling');
  });

  it('leaves out children with nothing planned today', async () => {
    const charlie = child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } });
    const dana = child({ id: 'child-2', name: { givenName: 'Dana', familyName: 'D' } });

    const listSchedule = vi.fn(async (childId: string) =>
      childId === 'child-1' ? [] : [occurrence({ slot: 'PickUp' })],
    );

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [charlie, dana]) },
      pickups: { listSchedule },
    });
    await settle(fixture);

    expect(tableRows(fixture).map((row) => row[0])).toEqual(['Dana']);
  });

  it('keeps each child’s guardian assignees resolved against that same child’s guardian list, not another child’s', async () => {
    const charlie = child({ id: 'child-1', name: { givenName: 'Charlie', familyName: 'C' } });
    const dana = child({ id: 'child-2', name: { givenName: 'Dana', familyName: 'D' } });

    const listSchedule = vi.fn(async (childId: string) =>
      childId === 'child-1'
        ? [occurrence({ assignee: { kind: 0, guardianId: 'guardian-1' }, slot: 'DropOff' })]
        : [occurrence({ assignee: { kind: 0, guardianId: 'guardian-1' }, slot: 'PickUp' })],
    );
    const listChildGuardians = vi.fn(async (childId: string) =>
      childId === 'child-1'
        ? [guardian({ id: 'guardian-1', name: { givenName: 'Gina', familyName: 'G' } })]
        : [guardian({ id: 'guardian-1', name: { givenName: 'Peter', familyName: 'P' } })],
    );

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [charlie, dana]), listChildGuardians },
      pickups: { listSchedule },
    });
    await settle(fixture);

    const rows = tableRows(fixture);
    expect(rows.find((row) => row[0] === 'Charlie')?.[1]).toContain('Gina');
    expect(rows.find((row) => row[0] === 'Dana')?.[2]).toContain('Peter');
  });

  // Holds every call open until released, tracking how many are in flight at once.
  function gatedCalls<T>(valueFor: (id: string) => T) {
    const waiting: (() => void)[] = [];
    let inFlight = 0;
    let maxInFlight = 0;

    return {
      call: (id: string) => {
        inFlight++;
        maxInFlight = Math.max(maxInFlight, inFlight);
        return new Promise<T>((resolve) =>
          waiting.push(() => {
            inFlight--;
            resolve(valueFor(id));
          }),
        );
      },
      releaseOne: () => waiting.shift()!(),
      get pending() {
        return waiting.length;
      },
      get maxInFlight() {
        return maxInFlight;
      },
    };
  }

  // Regression guard for the dashboard burst: two requests per child (schedule + guardians) used
  // to fire for every child at once. Now at most the cap's worth of children are in flight.
  it("caps concurrent per-child requests and still renders every child's pickups", async () => {
    const childCount = PER_ITEM_REQUEST_CONCURRENCY * 2 + 1;
    const children = Array.from({ length: childCount }, (_, i) =>
      child({ id: `child-${i}`, name: { givenName: `Kid${i}`, familyName: 'Test' } }),
    );
    const gate = gatedCalls(() => [occurrence({ assignee: { kind: 1 } })]);
    const listSchedule = vi.fn((childId: string) => gate.call(childId));
    const listChildGuardians = vi.fn(async () => [guardian()]);

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => children), listChildGuardians },
      pickups: { listSchedule },
    });
    await settle(fixture);

    expect(listSchedule).toHaveBeenCalledTimes(PER_ITEM_REQUEST_CONCURRENCY);
    expect(listChildGuardians).toHaveBeenCalledTimes(PER_ITEM_REQUEST_CONCURRENCY);

    while (gate.pending > 0) {
      gate.releaseOne();
      await settle(fixture);
    }

    expect(listSchedule).toHaveBeenCalledTimes(childCount);
    expect(listChildGuardians).toHaveBeenCalledTimes(childCount);
    expect(gate.maxInFlight).toBe(PER_ITEM_REQUEST_CONCURRENCY);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    children.forEach((c) => expect(text).toContain(c.name.givenName));
  });
});
