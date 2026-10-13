import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  ChecklistSuggestion,
  TaskLibraryService,
  TaskTemplate,
} from '../../../../../core/task-library.service';
import { settle } from '../../../../../../testing/onboarding-fixture';
import { SuggestionPanel } from './suggestion-panel';

describe('SuggestionPanel', () => {
  function suggestion(overrides: Partial<ChecklistSuggestion> = {}): ChecklistSuggestion {
    return {
      id: 'suggestion-1',
      title: 'Recorder',
      weekdays: ['Friday'],
      suggestedBy: 'child-1',
      status: 'Pending',
      suggestedAt: '2026-10-12T15:00:00Z',
      resolvedAt: null,
      subtaskId: null,
      ...overrides,
    };
  }

  function checklist(suggestions: ChecklistSuggestion[]): TaskTemplate {
    return {
      id: 'template-1',
      name: 'School bag',
      icon: '🎒',
      color: '#6366f1',
      subtasks: [],
      totalDurationMinutes: 0,
      isArchived: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      kind: 'Checklist',
      eveningBeforeFrom: null,
      suggestions,
    };
  }

  async function setup(
    suggestions: ChecklistSuggestion[],
    overrides: Partial<TaskLibraryService> = {},
  ) {
    const taskLibrary = {
      approveChecklistSuggestion: vi.fn().mockResolvedValue(checklist([])),
      declineChecklistSuggestion: vi.fn().mockResolvedValue(checklist([])),
      ...overrides,
    };

    await TestBed.configureTestingModule({
      imports: [SuggestionPanel],
      providers: [{ provide: TaskLibraryService, useValue: taskLibrary }],
    }).compileComponents();

    const fixture = TestBed.createComponent(SuggestionPanel);
    fixture.componentRef.setInput('template', checklist(suggestions));
    fixture.componentRef.setInput('childName', 'Emil');
    fixture.detectChanges();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const button = (text: string) =>
      Array.from(compiled.querySelectorAll<HTMLButtonElement>('button')).find((b) =>
        b.textContent?.includes(text),
      );

    return { fixture, compiled, taskLibrary, button };
  }

  it('renders nothing when no suggestion is pending', async () => {
    const { compiled } = await setup([suggestion({ status: 'Declined' })]);

    expect(compiled.querySelector('[data-testid="suggestion-panel"]')).toBeNull();
  });

  it('lists the pending suggestions with the child name and their days', async () => {
    const { compiled } = await setup([
      suggestion(),
      suggestion({ id: 'suggestion-2', title: 'Sun hat', weekdays: [] }),
    ]);

    const text = compiled.textContent ?? '';
    expect(text).toContain('Suggested by Emil');
    expect(text).toContain('Recorder');
    expect(text).toContain('Fri');
    expect(text).toContain('Sun hat');
    expect(text).toContain('Every day');
  });

  it('pre-fills the approve form from the suggestion and sends the guardian’s edits', async () => {
    const { fixture, compiled, taskLibrary, button } = await setup([suggestion()]);

    button('Approve')!.click();
    await settle(fixture);

    const input = compiled.querySelector<HTMLInputElement>('input[type="text"]')!;
    expect(input.value).toBe('Recorder');
    const friday = compiled.querySelector<HTMLButtonElement>('button[aria-label="Friday"]')!;
    expect(friday.getAttribute('aria-pressed')).toBe('true');

    input.value = 'Recorder and music';
    input.dispatchEvent(new Event('input'));
    compiled.querySelector<HTMLButtonElement>('button[aria-label="Monday"]')!.click();
    await settle(fixture);
    button('Add to list')!.click();
    await settle(fixture);

    expect(taskLibrary.approveChecklistSuggestion).toHaveBeenCalledWith(
      'template-1',
      'suggestion-1',
      'Recorder and music',
      ['Monday', 'Friday'],
    );
    expect(compiled.querySelector('input[type="text"]')).toBeNull();
  });

  it('cancels the approve form without calling the service', async () => {
    const { fixture, compiled, taskLibrary, button } = await setup([suggestion()]);

    button('Approve')!.click();
    await settle(fixture);
    button('Cancel')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[type="text"]')).toBeNull();
    expect(taskLibrary.approveChecklistSuggestion).not.toHaveBeenCalled();
  });

  it('declines with "Not now"', async () => {
    const { fixture, taskLibrary, button } = await setup([suggestion()]);

    button('Not now')!.click();
    await settle(fixture);

    expect(taskLibrary.declineChecklistSuggestion).toHaveBeenCalledWith(
      'template-1',
      'suggestion-1',
    );
  });

  it('shows an error when resolving fails', async () => {
    const { fixture, compiled, button } = await setup([suggestion()], {
      declineChecklistSuggestion: vi.fn().mockRejectedValue(new Error('409')),
    });

    button('Not now')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to resolve this suggestion.');
  });
});
