import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { FeelingsService } from '../../../../core/feelings.service';
import { FeelingsCheckInSettings } from './check-in-settings';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(el: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
}

describe('FeelingsCheckInSettings', () => {
  async function setup(promptTimes: string[] = ['07:30'], displayMode = 'FacesAndWords') {
    const feelings = {
      setPromptTimes: vi.fn(async (_: string, times: string[]) => times),
      setDisplayMode: vi.fn(async () => 'FacesOnly' as const),
    } satisfies Partial<FeelingsService>;

    await TestBed.configureTestingModule({
      imports: [FeelingsCheckInSettings],
      providers: [{ provide: FeelingsService, useValue: feelings }],
    }).compileComponents();

    const fixture = TestBed.createComponent(FeelingsCheckInSettings);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('childName', 'Ida');
    fixture.componentRef.setInput('promptTimes', promptTimes);
    fixture.componentRef.setInput('displayMode', displayMode);
    await settle(fixture);
    return { fixture, feelings, el: fixture.nativeElement as HTMLElement };
  }

  it('adds a reminder time and saves them, up to four', async () => {
    const { fixture, feelings, el } = await setup();
    const saved: string[][] = [];
    fixture.componentInstance.promptTimesSaved.subscribe((t) => saved.push(t));

    button(el, '+ Add a time')!.click();
    await settle(fixture);
    // A blank row can't be saved.
    expect(button(el, 'Save times')!.disabled).toBe(true);
    fixture.componentInstance['setRow'](1, '15:00');
    await settle(fixture);
    button(el, 'Save times')!.click();
    await settle(fixture);

    expect(feelings.setPromptTimes).toHaveBeenCalledWith('child-1', ['07:30', '15:00']);
    expect(saved).toEqual([['07:30', '15:00']]);
    expect(el.textContent).toContain('Saved.');

    button(el, '+ Add a time')!.click();
    button(el, '+ Add a time')!.click();
    await settle(fixture);
    expect(button(el, '+ Add a time')).toBeUndefined();
  });

  it('removes every reminder, which turns them off', async () => {
    const { fixture, feelings, el } = await setup();

    button(el, 'Remove')!.click();
    await settle(fixture);
    button(el, 'Save times')!.click();
    await settle(fixture);

    expect(feelings.setPromptTimes).toHaveBeenCalledWith('child-1', []);
  });

  it('switches the child’s buttons to faces only, naming the child', async () => {
    const { fixture, feelings, el } = await setup();
    const modes: string[] = [];
    fixture.componentInstance.displayModeSaved.subscribe((m) => modes.push(m));

    expect(el.textContent).toContain('Hide the words on Ida’s buttons');
    const toggle = el.querySelector('button[role="switch"]') as HTMLButtonElement;
    expect(toggle.getAttribute('aria-checked')).toBe('false');
    toggle.click();
    await settle(fixture);

    expect(feelings.setDisplayMode).toHaveBeenCalledWith('child-1', 'FacesOnly');
    expect(modes).toEqual(['FacesOnly']);
  });

  it('shows faces only as on, and switches it back off', async () => {
    const { fixture, feelings, el } = await setup([], 'FacesOnly');

    const toggle = el.querySelector('button[role="switch"]') as HTMLButtonElement;
    expect(toggle.getAttribute('aria-checked')).toBe('true');
    toggle.click();
    await settle(fixture);

    expect(feelings.setDisplayMode).toHaveBeenCalledWith('child-1', 'FacesAndWords');
  });
});
