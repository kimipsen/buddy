import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { EMPTY_SETUP, OnboardingSetup } from '../../../../core/onboarding.service';
import {
  calendarDetail,
  child,
  settle,
  setupWith,
} from '../../../../../testing/onboarding-fixture';
import { SummaryStep } from './summary-step';
import { disableFeatures } from '../../../../../testing/features-fixture';

describe('SummaryStep', () => {
  async function renderElement(setup: OnboardingSetup, invitationsSkipped = false) {
    await TestBed.configureTestingModule({
      imports: [SummaryStep],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(SummaryStep);
    fixture.componentRef.setInput('setup', setup);
    fixture.componentRef.setInput('invitationsSkipped', invitationsSkipped);
    await settle(fixture);
    return fixture.nativeElement as HTMLElement;
  }

  async function render(setup: OnboardingSetup, invitationsSkipped = false) {
    return (await renderElement(setup, invitationsSkipped)).textContent ?? '';
  }

  it('lists what the setup created', async () => {
    const text = await render(
      setupWith({
        children: [child('c1', 'Ada'), child('c2', 'Emil')],
        pendingInvites: [
          { id: 'i1', email: 'aunt@buddy.test', role: 'Admin', invitedAt: '', expiresAt: '' },
        ],
        calendars: [calendarDetail()],
        hasScheduledRoutine: true,
        hasMealAssignment: true,
      }),
    );

    expect(text).toContain('The Hansens');
    expect(text).toMatch(/Ada,\s+Emil/);
    expect(text).toContain('aunt@buddy.test');
    expect(text).toContain('Family');
    expect(text).toContain('Scheduled');
    expect(text).toContain('Planned');
  });

  it('says what is missing or skipped', async () => {
    const text = await render(EMPTY_SETUP, true);

    expect(text).toContain('Skipped');
    expect(text).toContain('None');
  });

  it('leaves out the routine and meal rows while their features are off', async () => {
    disableFeatures('taskLibrary', 'mealplans');
    const text = await render(setupWith({ hasScheduledRoutine: true, hasMealAssignment: true }));

    expect(text).not.toContain('Scheduled');
    expect(text).not.toContain('Planned');
    expect(text).toContain('The Hansens');
  });

  it('suggests turning on heads-ups, which are off for every child until a guardian does', async () => {
    const element = await renderElement(EMPTY_SETUP);

    const hint = element.querySelector('[data-testid="warnings-hint"]');
    expect(hint?.textContent).toContain('Want Buddy to warn your child before changes?');
    const link = hint?.querySelector('a');
    expect(link?.textContent?.trim()).toBe('Turn on heads-ups');
    expect(link?.getAttribute('href')).toBe('/guardian/transition-warnings');
  });

  it('leaves out the heads-up hint while transition warnings are off', async () => {
    disableFeatures('transitionWarnings');
    const element = await renderElement(EMPTY_SETUP);

    expect(element.querySelector('[data-testid="warnings-hint"]')).toBeNull();
  });
});
