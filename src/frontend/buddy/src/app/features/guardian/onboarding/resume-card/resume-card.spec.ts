import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { OnboardingService, OnboardingStatus } from '../../../../core/onboarding.service';
import { progress, settle } from '../../../../../testing/onboarding-fixture';
import { OnboardingResumeCard } from './resume-card';

describe('OnboardingResumeCard', () => {
  async function render(getProgress: OnboardingService['getProgress']) {
    await TestBed.configureTestingModule({
      imports: [OnboardingResumeCard],
      providers: [provideRouter([]), { provide: OnboardingService, useValue: { getProgress } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(OnboardingResumeCard);
    await settle(fixture);
    return fixture.nativeElement as HTMLElement;
  }

  it('links back into a deferred guide', async () => {
    const compiled = await render(vi.fn(async () => progress({ status: 'Deferred' })));

    const link = compiled.querySelector('a');
    expect(link?.textContent?.trim()).toBe('Resume setup');
    expect(link?.getAttribute('href')).toBe('/guardian/onboarding');
  });

  it('is a full-width banner with its own space below it, not a grid item', async () => {
    const compiled = await render(vi.fn(async () => progress({ status: 'Deferred' })));

    const banner = compiled.querySelector('section') as HTMLElement;
    expect(banner.classList).toContain('mb-6');
    expect(banner.classList).not.toContain('lg:col-span-2');
  });

  it.each(['NotStarted', 'Active', 'Completed'] as OnboardingStatus[])(
    'shows nothing for status %s',
    async (status) => {
      const compiled = await render(vi.fn(async () => progress({ status })));

      expect(compiled.textContent?.trim()).toBe('');
    },
  );

  it('shows nothing when the progress cannot be read', async () => {
    const compiled = await render(
      vi.fn(async () => {
        throw new Error('network');
      }),
    );

    expect(compiled.textContent?.trim()).toBe('');
  });
});
