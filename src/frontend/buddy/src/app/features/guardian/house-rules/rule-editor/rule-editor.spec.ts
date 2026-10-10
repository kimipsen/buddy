import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { Rule } from '../../../../core/house-rules.service';
import { RuleEditor, RuleEditorResult } from './rule-editor';

const RULE: Rule = {
  id: 'r1',
  title: 'Dinner',
  body: 'No phones',
  revision: 2,
  acknowledgementRevision: 2,
  lastEditedAt: '2026-10-01T10:00:00Z',
  acknowledgements: [],
};

describe('RuleEditor', () => {
  async function setup(
    rule: Rule | null = null,
    inputs: { saving?: boolean; error?: string } = {},
  ) {
    await TestBed.configureTestingModule({ imports: [RuleEditor] }).compileComponents();

    const fixture: ComponentFixture<RuleEditor> = TestBed.createComponent(RuleEditor);
    fixture.componentRef.setInput('rule', rule);
    fixture.componentRef.setInput('saving', inputs.saving ?? false);
    fixture.componentRef.setInput('error', inputs.error ?? null);
    const saved = vi.fn<(result: RuleEditorResult) => void>();
    const closed = vi.fn();
    fixture.componentInstance.save.subscribe(saved);
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    await fixture.whenStable();

    return { fixture, compiled: fixture.nativeElement as HTMLElement, saved, closed };
  }

  function saveButton(compiled: HTMLElement): HTMLButtonElement {
    return compiled.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  function type(fixture: ComponentFixture<RuleEditor>, selector: string, value: string): void {
    const element = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      selector,
    )!;
    element.value = value;
    element.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('is a labelled modal dialog that starts on the title', async () => {
    const { compiled } = await setup();

    const dialog = compiled.querySelector('[role="dialog"]')!;
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(compiled.querySelector('#rule-editor-title')?.textContent?.trim()).toBe('New rule');
    expect(document.activeElement?.id).toBe('rule-editor-title-input');
  });

  it('needs a title before it can save, and has no small-fix switch for a new rule', async () => {
    const { fixture, compiled, saved } = await setup();

    expect(saveButton(compiled).disabled).toBe(true);
    expect(compiled.querySelector('button[role="switch"]')).toBeNull();

    type(fixture, '#rule-editor-title-input', '   ');
    expect(saveButton(compiled).disabled).toBe(true);

    type(fixture, '#rule-editor-title-input', ' Shoes ');
    type(fixture, '#rule-editor-body', '**Off**');
    saveButton(compiled).click();

    expect(saved).toHaveBeenCalledWith({
      content: { title: 'Shoes', body: '**Off**' },
      requireReacknowledgement: true,
    });
  });

  it('prefills an existing rule and sends a small fix without re-acknowledgement', async () => {
    const { fixture, compiled, saved } = await setup(RULE);

    expect(compiled.querySelector('#rule-editor-title')?.textContent?.trim()).toBe('Edit rule');
    expect(compiled.querySelector<HTMLInputElement>('#rule-editor-title-input')!.value).toBe(
      'Dinner',
    );
    expect(compiled.querySelector<HTMLTextAreaElement>('#rule-editor-body')!.value).toBe(
      'No phones',
    );

    compiled.querySelector<HTMLButtonElement>('button[role="switch"]')!.click();
    fixture.detectChanges();
    saveButton(compiled).click();

    expect(saved).toHaveBeenCalledWith({
      content: { title: 'Dinner', body: 'No phones' },
      requireReacknowledgement: false,
    });
  });

  it('refuses a body over the limit', async () => {
    const { fixture, compiled } = await setup(RULE);

    type(fixture, '#rule-editor-body', 'x'.repeat(4001));

    expect(saveButton(compiled).disabled).toBe(true);
  });

  it('closes on Escape and on Cancel, but not while saving', async () => {
    const { compiled, closed } = await setup();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    [...compiled.querySelectorAll('button')]
      .find((b) => b.textContent?.trim() === 'Cancel')!
      .click();
    expect(closed).toHaveBeenCalledTimes(2);

    TestBed.resetTestingModule();
    const busy = await setup(null, { saving: true });
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(busy.closed).not.toHaveBeenCalled();
    expect(saveButton(busy.compiled).textContent?.trim()).toBe('Saving…');
  });

  it('shows the error it is given', async () => {
    const { compiled } = await setup(RULE, { error: 'houseRules.saveError' });

    expect(compiled.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Unable to save the rule.',
    );
  });
});
