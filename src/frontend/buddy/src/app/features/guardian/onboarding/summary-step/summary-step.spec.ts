import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { EMPTY_SETUP, OnboardingSetup } from '../../../../core/onboarding.service';
import {
  calendarDetail,
  child,
  settle,
  setupWith,
} from '../../../../../testing/onboarding-fixture';
import { SummaryStep } from './summary-step';

describe('SummaryStep', () => {
  async function render(setup: OnboardingSetup, invitationsSkipped = false) {
    await TestBed.configureTestingModule({ imports: [SummaryStep] }).compileComponents();
    const fixture = TestBed.createComponent(SummaryStep);
    fixture.componentRef.setInput('setup', setup);
    fixture.componentRef.setInput('invitationsSkipped', invitationsSkipped);
    await settle(fixture);
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
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
});
