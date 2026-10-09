import { ComponentFixture } from '@angular/core/testing';

import { CalendarDetail } from '../app/core/calendars.service';
import { GroupDetail } from '../app/core/groups.service';
import { ChildSummary } from '../app/core/guardians.service';
import { EMPTY_SETUP, OnboardingProgress, OnboardingSetup } from '../app/core/onboarding.service';
import { TaskTemplate } from '../app/core/task-library.service';

// Shared data and DOM helpers for the guardian onboarding guide's specs.

export function progress(overrides: Partial<OnboardingProgress> = {}): OnboardingProgress {
  return {
    status: 'Active',
    setupGroupId: 'group-1',
    invitationsSkipped: false,
    version: 1,
    ...overrides,
  };
}

export function child(id = 'child-1', givenName = 'Ada'): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Hansen' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'UTC',
  };
}

export function groupDetail(overrides: Partial<GroupDetail> = {}): GroupDetail {
  return {
    id: 'group-1',
    name: 'The Hansens',
    members: [
      { userId: 'me', givenName: 'Sara', familyName: 'Hansen', role: 'Owner', isChild: false },
    ],
    calendarPermissionPolicy: { Owner: 'Owner', Admin: 'Contributor', Member: 'Viewer' },
    mealplanPermissionPolicy: { Owner: 'Manage', Admin: 'Manage', Member: 'None' },
    medicinePermissionPolicy: { Owner: 'Manage', Admin: 'Mark', Member: 'None' },
    ...overrides,
  };
}

export function calendarDetail(overrides: Partial<CalendarDetail> = {}): CalendarDetail {
  return {
    id: 'cal-1',
    name: 'Family',
    icon: '📅',
    timeZoneId: 'UTC',
    groupId: 'group-1',
    members: [],
    ...overrides,
  };
}

export function taskTemplate(overrides: Partial<TaskTemplate> = {}): TaskTemplate {
  return {
    id: 'template-1',
    name: 'Morning routine',
    icon: '🌅',
    color: '#10b981',
    subtasks: [],
    totalDurationMinutes: 0,
    isArchived: false,
    createdBy: 'me',
    lastModifiedBy: 'me',
    ...overrides,
  };
}

export function setupWith(overrides: Partial<OnboardingSetup> = {}): OnboardingSetup {
  return { ...EMPTY_SETUP, group: groupDetail(), ...overrides };
}

// The app is zoneless and the stubs return plain promises, so flush macrotasks rather than rely on
// whenStable(); repeated for handlers that chain several awaits.
export async function settle(fixture: ComponentFixture<unknown>, rounds = 5): Promise<void> {
  for (let round = 0; round < rounds; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

export function buttonByText(root: ParentNode, text: string): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find(
    (button) => button.textContent?.trim() === text,
  );
}

export function typeInto(input: HTMLInputElement | HTMLSelectElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
}

export function submit(root: ParentNode, selector = 'form'): void {
  root.querySelector<HTMLFormElement>(selector)!.dispatchEvent(new Event('submit'));
}
