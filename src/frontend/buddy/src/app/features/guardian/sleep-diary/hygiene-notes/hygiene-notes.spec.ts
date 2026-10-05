import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { SleepDiaryService } from '../../../../core/sleep-diary.service';
import { HygieneNotes } from './hygiene-notes';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

describe('HygieneNotes', () => {
  async function setup(service: Partial<SleepDiaryService> = {}) {
    const stub: Partial<SleepDiaryService> = {
      updateHygieneNotes: vi.fn(async () => undefined),
      ...service,
    };

    await TestBed.configureTestingModule({
      imports: [HygieneNotes],
      providers: [{ provide: SleepDiaryService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(HygieneNotes);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('notes', 'Curtains');
    const saved = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const textarea = compiled.querySelector('textarea')!;
    const save = compiled.querySelector('button') as HTMLButtonElement;

    return { fixture, compiled, textarea, save, saved, stub };
  }

  it('shows the saved notes and only enables saving after a change', async () => {
    const { fixture, textarea, save } = await setup();

    expect(textarea.value).toBe('Curtains');
    expect(save.disabled).toBe(true);

    textarea.value = 'Curtains, no screens';
    textarea.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(save.disabled).toBe(false);
  });

  it('saves trimmed notes and confirms', async () => {
    const { fixture, compiled, textarea, save, saved, stub } = await setup();

    textarea.value = '  No screens after 19:00  ';
    textarea.dispatchEvent(new Event('input'));
    await settle(fixture);
    save.click();
    await settle(fixture);

    expect(stub.updateHygieneNotes).toHaveBeenCalledWith('child-1', 'No screens after 19:00');
    expect(saved).toHaveBeenCalledWith('No screens after 19:00');
    // The parent passes the saved notes back in.
    fixture.componentRef.setInput('notes', 'No screens after 19:00');
    await settle(fixture);
    expect(compiled.querySelector('[role="status"]')?.textContent?.trim()).toBe('Saved.');
  });

  it('shows an error when saving fails', async () => {
    const { fixture, compiled, textarea, save, saved } = await setup({
      updateHygieneNotes: vi.fn(async () => Promise.reject(new Error('boom'))),
    });

    textarea.value = 'x';
    textarea.dispatchEvent(new Event('input'));
    await settle(fixture);
    save.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save the notes.');
    expect(saved).not.toHaveBeenCalled();
  });
});
