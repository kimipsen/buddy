import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { ProgressService } from '../../../core/progress.service';
import { ChildrenOverview } from './children-overview';

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
      getChildProgress: vi.fn(async () => ({ totalStars: 0, unlockedMilestones: [] })),
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

    expect(items.length).toBe(2);
    expect(compiled.textContent).toContain('Sam Kid');
    expect(compiled.textContent).toContain('Alex Kid');

    const badges = compiled.querySelectorAll('li span:last-child');
    expect(badges.length).toBe(2);
    badges.forEach((badge) => expect(badge.textContent).toContain('Linked'));
  });

  it('does not show the empty state or an error once children load successfully', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => [child()]) } });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('No children linked yet.');
    expect(compiled.textContent).not.toContain('Unable to load children.');
  });
});
