import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  ChecklistSuggestion,
  TaskLibraryService,
  TaskTemplate,
} from '../../../../core/task-library.service';
import { buttonByText, settle } from '../../../../../testing/onboarding-fixture';
import { ChildSuggestions } from './child-suggestions';

describe('ChildSuggestions', () => {
  const now = Date.parse('2026-10-13T18:00:00Z');
  const DAY = 24 * 60 * 60 * 1000;

  function suggestion(overrides: Partial<ChecklistSuggestion> = {}): ChecklistSuggestion {
    return {
      id: 'suggestion-1',
      title: 'Recorder',
      weekdays: [],
      suggestedBy: 'child-1',
      status: 'Pending',
      suggestedAt: '2026-10-12T15:00:00Z',
      resolvedAt: null,
      subtaskId: null,
      ...overrides,
    };
  }

  function checklist(suggestions: ChecklistSuggestion[] = []): TaskTemplate {
    return {
      id: 'bag-1',
      name: 'School bag',
      icon: '🎒',
      color: '#6366f1',
      subtasks: [],
      totalDurationMinutes: 0,
      isArchived: false,
      createdBy: 'guardian-1',
      lastModifiedBy: 'guardian-1',
      kind: 'Checklist',
      eveningBeforeFrom: '17:00',
      suggestions,
    };
  }

  async function setup(
    suggestions: ChecklistSuggestion[] = [],
    taskLibrary: Partial<TaskLibraryService> = {},
  ) {
    const stub: Partial<TaskLibraryService> = {
      suggestChecklistItem: vi.fn(async () => checklist([suggestion()])),
      withdrawChecklistSuggestion: vi.fn(async () =>
        checklist([suggestion({ status: 'Withdrawn' })]),
      ),
      ...taskLibrary,
    };

    await TestBed.configureTestingModule({
      imports: [ChildSuggestions],
      providers: [{ provide: TaskLibraryService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(ChildSuggestions);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    fixture.componentRef.setInput('template', checklist(suggestions));
    fixture.componentRef.setInput('now', now);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, stub, changed };
  }

  async function fillAndSend(
    fixture: Awaited<ReturnType<typeof setup>>['fixture'],
    compiled: HTMLElement,
    title: string,
  ) {
    buttonByText(compiled, 'Suggest something')!.click();
    await settle(fixture);
    const input = compiled.querySelector<HTMLInputElement>('input[type="text"]')!;
    input.value = title;
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  it('suggests a title and the chosen days, then closes the sheet and emits the template', async () => {
    const { fixture, compiled, stub, changed } = await setup();

    await fillAndSend(fixture, compiled, '  Recorder ');
    compiled.querySelector<HTMLButtonElement>('button[aria-label="Friday"]')!.click();
    await settle(fixture);
    buttonByText(compiled, 'Send to a grown-up')!.click();
    await settle(fixture);

    expect(stub.suggestChecklistItem).toHaveBeenCalledWith('bag-1', 'Recorder', ['Friday']);
    expect(changed).toHaveBeenCalledWith(checklist([suggestion()]));
    expect(compiled.querySelector('form')).toBeNull();
  });

  it('keeps Send disabled until a title is typed', async () => {
    const { fixture, compiled } = await setup();

    buttonByText(compiled, 'Suggest something')!.click();
    await settle(fixture);

    expect(buttonByText(compiled, 'Send to a grown-up')!.disabled).toBe(true);
    expect(compiled.textContent).toContain('What do you need to bring?');
    expect(compiled.textContent).toContain('Which days? (optional)');
  });

  it('cancels the sheet', async () => {
    const { fixture, compiled, stub } = await setup();

    await fillAndSend(fixture, compiled, 'Recorder');
    buttonByText(compiled, 'Cancel')!.click();
    await settle(fixture);

    expect(compiled.querySelector('form')).toBeNull();
    expect(stub.suggestChecklistItem).not.toHaveBeenCalled();
  });

  it.each([
    ['checklist_item_exists', 'That’s already on your list'],
    ['too_many_pending_suggestions', 'Wait for a grown-up to look at your ideas first'],
    ['something_else', 'That didn’t work. Try again.'],
  ])('shows the %s conflict in the child’s words', async (code, message) => {
    const { fixture, compiled } = await setup([], {
      suggestChecklistItem: vi
        .fn()
        .mockRejectedValue(new HttpErrorResponse({ status: 409, error: { code } })),
    });

    await fillAndSend(fixture, compiled, 'Lunchbox');
    buttonByText(compiled, 'Send to a grown-up')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(message);
    expect(compiled.querySelector('form')).not.toBeNull();
  });

  it('shows a pending suggestion as waiting, and takes it back', async () => {
    const { fixture, compiled, stub, changed } = await setup([suggestion()]);

    expect(compiled.textContent).toContain('Recorder · Waiting for a grown-up');

    compiled.querySelector<HTMLButtonElement>('button[aria-label="Take back Recorder"]')!.click();
    await settle(fixture);

    expect(stub.withdrawChecklistSuggestion).toHaveBeenCalledWith('bag-1', 'suggestion-1');
    expect(changed).toHaveBeenCalled();
  });

  it('shows what a grown-up decided for three days, and nothing for a withdrawn idea', async () => {
    const { compiled } = await setup([
      suggestion({
        id: 'a',
        title: 'Recorder',
        status: 'Approved',
        resolvedAt: new Date(now - DAY).toISOString(),
      }),
      suggestion({
        id: 'b',
        title: 'Sweets',
        status: 'Declined',
        resolvedAt: new Date(now - 2 * DAY).toISOString(),
      }),
      suggestion({
        id: 'c',
        title: 'Old idea',
        status: 'Approved',
        resolvedAt: new Date(now - 3 * DAY - 1).toISOString(),
      }),
      suggestion({
        id: 'd',
        title: 'Sun hat',
        status: 'Withdrawn',
        resolvedAt: new Date(now - DAY).toISOString(),
      }),
    ]);

    const text = compiled.textContent ?? '';
    expect(text).toContain('Recorder · Added to your list');
    expect(text).toContain('Sweets · Not added this time');
    expect(text).not.toContain('Old idea');
    expect(text).not.toContain('Sun hat');
  });
});
