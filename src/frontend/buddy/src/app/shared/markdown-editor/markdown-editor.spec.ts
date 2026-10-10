import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { MarkdownEditor } from './markdown-editor';

describe('MarkdownEditor', () => {
  async function setup(
    value = '',
  ): Promise<{ fixture: ComponentFixture<MarkdownEditor>; compiled: HTMLElement }> {
    await TestBed.configureTestingModule({ imports: [MarkdownEditor] }).compileComponents();

    const fixture = TestBed.createComponent(MarkdownEditor);
    fixture.componentRef.setInput('inputId', 'body');
    fixture.componentRef.setInput('label', 'The rule');
    fixture.componentRef.setInput('hint', 'Markdown works');
    fixture.componentRef.setInput('maxLength', 20);
    fixture.componentInstance.value.set(value);
    fixture.detectChanges();
    await fixture.whenStable();

    return { fixture, compiled: fixture.nativeElement as HTMLElement };
  }

  function textarea(compiled: HTMLElement): HTMLTextAreaElement {
    return compiled.querySelector('textarea')!;
  }

  it('labels the textarea and ties the hint to it', async () => {
    const { compiled } = await setup('Hello');

    expect(compiled.querySelector('label')?.getAttribute('for')).toBe('body');
    expect(textarea(compiled).value).toBe('Hello');
    expect(textarea(compiled).getAttribute('aria-describedby')).toBe('body-hint');
    expect(compiled.querySelector('#body-hint')?.textContent?.trim()).toBe('Markdown works');
  });

  it('writes typed text back to the value', async () => {
    const { fixture, compiled } = await setup();

    textarea(compiled).value = 'No phones';
    textarea(compiled).dispatchEvent(new Event('input'));

    expect(fixture.componentInstance.value()).toBe('No phones');
  });

  it('applies a toolbar action at the selection', async () => {
    const { fixture, compiled } = await setup('say never');
    textarea(compiled).setSelectionRange(4, 9);

    compiled.querySelector<HTMLButtonElement>('[data-action="bold"]')!.click();

    expect(fixture.componentInstance.value()).toBe('say **never**');
  });

  it('labels every toolbar button', async () => {
    const { compiled } = await setup();

    const labels = [...compiled.querySelectorAll('[role="toolbar"] button')].map((b) =>
      b.getAttribute('aria-label'),
    );
    expect(labels).toEqual([
      'Bold',
      'Bulleted list',
      'Numbered list',
      'Checklist',
      'Table',
      'Link',
    ]);
  });

  it('shows the rendered markdown in preview mode', async () => {
    const { fixture, compiled } = await setup('**Bold** rule');

    [...compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]')]
      .find((b) => b.textContent?.includes('Preview'))!
      .click();
    fixture.detectChanges();

    expect(compiled.querySelector('textarea')).toBeNull();
    expect(compiled.querySelector('[data-testid="markdown-preview"] strong')?.textContent).toBe(
      'Bold',
    );
  });

  it('says there is nothing to preview for an empty body', async () => {
    const { fixture, compiled } = await setup('  ');

    [...compiled.querySelectorAll<HTMLButtonElement>('[role="radio"]')]
      .find((b) => b.textContent?.includes('Preview'))!
      .click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Nothing to preview yet.');
  });

  it('counts characters and flags going over the limit', async () => {
    const { fixture, compiled } = await setup('short');

    expect(compiled.textContent).toContain('5 / 20 characters');
    expect(textarea(compiled).getAttribute('aria-invalid')).toBe('false');

    fixture.componentInstance.value.set('x'.repeat(21));
    fixture.detectChanges();

    expect(compiled.textContent).toContain('21 / 20 characters');
    expect(textarea(compiled).getAttribute('aria-invalid')).toBe('true');
  });
});
