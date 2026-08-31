import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GuardiansService } from '../../../core/guardians.service';
import { ProgressService } from '../../../core/progress.service';
import { GuardianProgress } from './progress';

// GuardianProgress is a trivial shell: a back link plus <app-manage-progress-goals>, no logic of
// its own. This smoke test only confirms it renders that composition -- ManageProgressGoals' own
// behavior is covered by manage-progress-goals.spec.ts. Its child services still need stubs here,
// since mounting the real ManageProgressGoals otherwise instantiates the real (HttpClient-backed)
// services via DI.
describe('GuardianProgress', () => {
  async function setup() {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [])
    };
    const progressStub: Partial<ProgressService> = {
      getChildProgress: vi.fn(async () => ({ totalStars: 0, unlockedMilestones: [], currentIcon: null, nextGoalThreshold: 0, nextGoalIcon: '🌱', goalPosts: [] }))
    };

    await TestBed.configureTestingModule({
      imports: [GuardianProgress],
      providers: [provideRouter([]), { provide: GuardiansService, useValue: guardiansStub }, { provide: ProgressService, useValue: progressStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianProgress);

    return { fixture };
  }

  it('renders the manage-progress-goals panel and a back link to the guardian home', async () => {
    const { fixture } = await setup();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-manage-progress-goals')).toBeTruthy();
    expect(compiled.querySelector('a[href="/guardian"]')).toBeTruthy();
  });
});
