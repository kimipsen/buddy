import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { ProgressService, ProgressSummary } from '../../../core/progress.service';
import { ChildrenOverview, PROGRESS_REQUEST_CONCURRENCY } from './children-overview';

describe('ChildrenOverview', () => {
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

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    progress?: Partial<ProgressService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => []),
      ...stubs.guardians
    };
    const progressStub: Partial<ProgressService> = {
      getChildProgress: vi.fn(async () => ({ totalStars: 0, unlockedMilestones: [], currentIcon: null, nextGoalThreshold: 5, nextGoalIcon: '🌱', goalPosts: [] })),
      ...stubs.progress
    };

    await TestBed.configureTestingModule({
      imports: [ChildrenOverview],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: ProgressService, useValue: progressStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildrenOverview);

    return { fixture, guardians: guardiansStub, progress: progressStub };
  }

  // loadChildren chains an await on the stubbed service call before the signals driving the
  // template settle -- a single whenStable() flush isn't always enough, so flush a generous fixed
  // number of times rather than guessing when it's "probably" done.
  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  it('shows the loading spinner while children are loading', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('shows the empty state once loading finishes with no children', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeFalsy();
    expect(compiled.textContent).toContain('No children linked yet. Add one from Settings.');
  });

  it('shows the translated error message when loading children fails', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeFalsy();
    expect(compiled.textContent).toContain('Unable to load children.');
    expect(compiled.textContent).not.toContain('No children linked yet.');
  });

  it('renders each child with their full name and a linked badge', async () => {
    const children = [
      child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }),
      child({ id: 'child-2', name: { givenName: 'Alex', familyName: 'Kid' } })
    ];

    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => children) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const items = compiled.querySelectorAll('li');

    expect(items).toHaveLength(2);
    expect(compiled.textContent).toContain('Sam Kid');
    expect(compiled.textContent).toContain('Alex Kid');

    // Direct-child combinators, not a bare `span:last-child` -- the wrapper span around the
    // badges is itself a last-child of <li>, and (see the next test) the "Linked" span can have a
    // star-badge sibling before it, so an unscoped selector matches extra spans it shouldn't.
    const badges = compiled.querySelectorAll('li > span:last-child > span:last-child');
    expect(badges).toHaveLength(2);
    badges.forEach((badge) => expect(badge.textContent).toContain('Linked'));
  });

  it('shows exactly one linked badge per child even when a star badge renders alongside it', async () => {
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => [child()]) },
      progress: { getChildProgress: vi.fn(async () => ({ totalStars: 3, unlockedMilestones: [], currentIcon: '🌱', nextGoalThreshold: 5, nextGoalIcon: '🌿', goalPosts: [] })) }
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('3');
    // The child's current goalpost icon, not a hardcoded star -- see progress-badge.ts for the
    // same currentIcon-falls-back-to-nextGoalIcon resolution.
    expect(compiled.textContent).toContain('🌱');

    const badges = compiled.querySelectorAll('li > span:last-child > span:last-child');
    expect(badges).toHaveLength(1);
    expect(badges[0].textContent).toContain('Linked');
  });

  it('does not show the empty state or an error once children load successfully', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('No children linked yet.');
    expect(compiled.textContent).not.toContain('Unable to load children.');
  });
  function summary(totalStars: number): ProgressSummary {
    return { totalStars, unlockedMilestones: [], currentIcon: '🌱', nextGoalThreshold: 5, nextGoalIcon: '🌿', goalPosts: [] };
  }

  // Regression guard for the dashboard N+1 burst: a guardian with many children used to fire one
  // /progress/children/{id} request per child all at once, which exhausted the API's Postgres
  // pools. There's still one request per child, but never more than the cap in flight.
  it('caps concurrent progress requests and still loads a badge for every child', async () => {
    const childCount = PROGRESS_REQUEST_CONCURRENCY * 3 + 1;
    const children = Array.from({ length: childCount }, (_, i) =>
      child({ id: `child-${i}`, name: { givenName: `Kid${i}`, familyName: 'Test' } })
    );
    const pending = new Map<string, (value: ProgressSummary) => void>();
    let inFlight = 0;
    let maxInFlight = 0;
    const getChildProgress = vi.fn((childId: string) => {
      inFlight++;
      maxInFlight = Math.max(maxInFlight, inFlight);
      return new Promise<ProgressSummary>((resolve) => {
        pending.set(childId, (value) => {
          inFlight--;
          resolve(value);
        });
      });
    });

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => children) },
      progress: { getChildProgress }
    });
    await settle(fixture);

    expect(getChildProgress).toHaveBeenCalledTimes(PROGRESS_REQUEST_CONCURRENCY);

    // Drain: resolve whatever is in flight until every child has been requested and answered.
    let answered = 0;
    while (answered < childCount) {
      const [id, resolve] = pending.entries().next().value!;
      pending.delete(id);
      resolve(summary(Number(id.split('-')[1]) + 100));
      answered++;
      await settle(fixture);
    }

    expect(getChildProgress).toHaveBeenCalledTimes(childCount);
    expect(new Set(getChildProgress.mock.calls.map(([id]) => id)).size).toBe(childCount);
    expect(maxInFlight).toBe(PROGRESS_REQUEST_CONCURRENCY);

    const compiled = fixture.nativeElement as HTMLElement;
    const items = Array.from(compiled.querySelectorAll('li'));
    expect(items).toHaveLength(childCount);
    items.forEach((item, i) => expect(item.textContent).toContain(String(i + 100)));
  });

  it('still shows the other children\'s badges when one child\'s progress fails', async () => {
    const children = [
      child({ id: 'child-1', name: { givenName: 'Sam', familyName: 'Kid' } }),
      child({ id: 'child-2', name: { givenName: 'Alex', familyName: 'Kid' } })
    ];

    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(async () => children) },
      progress: {
        getChildProgress: vi.fn(async (childId: string) => {
          if (childId === 'child-1') {
            throw new Error('boom');
          }
          return summary(7);
        })
      }
    });
    await settle(fixture);

    const [sam, alex] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));
    expect(sam.textContent).not.toContain('🌱');
    expect(alex.textContent).toContain('7');
    expect(alex.textContent).toContain('🌱');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Unable to load children.');
  });
});
