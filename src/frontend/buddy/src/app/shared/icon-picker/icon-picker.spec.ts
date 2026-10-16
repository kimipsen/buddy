import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { TranslationService } from '../../core/i18n/translation.service';
import { PICTOGRAMS } from '../../core/pictograms';
import { IconPicker } from './icon-picker';

describe('IconPicker', () => {
  async function setup(value = '') {
    await TestBed.configureTestingModule({ imports: [IconPicker] }).compileComponents();

    const fixture = TestBed.createComponent(IconPicker);
    const onValueChange = vi.fn();
    fixture.componentInstance.value.subscribe(onValueChange);
    fixture.componentRef.setInput('name', 'stepIcon');
    fixture.componentRef.setInput('label', 'Icon');
    fixture.componentRef.setInput('placeholder', '📋');
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const trigger = () =>
      [...host.querySelectorAll('button')].find((b) => b.textContent?.includes('Pictures'))!;
    const tiles = () => [...host.querySelectorAll<HTMLButtonElement>('[role="dialog"] button')];

    return { fixture, host, onValueChange, trigger, tiles };
  }

  it('shows the emoji input with the caller’s name, label and placeholder', async () => {
    const { host } = await setup('🪥');

    const input = host.querySelector<HTMLInputElement>('input[name="stepIcon"]')!;
    expect(input.value).toBe('🪥');
    expect(input.getAttribute('aria-label')).toBe('Icon');
    expect(input.placeholder).toBe('📋');
  });

  it('emits a typed emoji', async () => {
    const { host, onValueChange } = await setup();

    const input = host.querySelector<HTMLInputElement>('input[name="stepIcon"]')!;
    input.value = '🚿';
    input.dispatchEvent(new Event('input'));

    expect(onValueChange).toHaveBeenLastCalledWith('🚿');
  });

  it('opens a grid of every pictogram, sorted by name, and focuses the search', async () => {
    const { fixture, host, trigger, tiles } = await setup();

    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    trigger().click();
    fixture.detectChanges();
    await fixture.whenRenderingDone();

    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    expect(tiles()).toHaveLength(PICTOGRAMS.length);
    expect(tiles()[0].textContent?.trim()).toBe('Bath');
    expect(document.activeElement).toBe(host.querySelector('input[type="search"]'));
  });

  it('filters by the translated name and says when nothing matches', async () => {
    const { fixture, host, trigger, tiles } = await setup();
    trigger().click();
    fixture.detectChanges();

    const search = host.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'TEETH';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(tiles().map((t) => t.textContent?.trim())).toEqual(['Brush teeth']);

    search.value = 'zebra';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(tiles()).toHaveLength(0);
    expect(host.textContent).toContain('No pictures match.');
  });

  it('searches the Danish names once the language is Danish', async () => {
    const { fixture, host, trigger, tiles } = await setup();
    await TestBed.inject(TranslationService).setLanguage('da');
    trigger().click();
    fixture.detectChanges();

    const search = host.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'tænder';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(tiles().map((t) => t.textContent?.trim())).toEqual(['Børste tænder']);
  });

  it('picks a pictogram, closes and shows it in place of the input', async () => {
    const { fixture, host, onValueChange, trigger, tiles } = await setup();
    trigger().click();
    fixture.detectChanges();

    tiles()
      .find((t) => t.textContent?.includes('Brush teeth'))!
      .click();
    fixture.detectChanges();

    expect(onValueChange).toHaveBeenLastCalledWith('pictogram:brush-teeth');
    expect(host.querySelector('[role="dialog"]')).toBeNull();
    expect(host.querySelector('input[name="stepIcon"]')).toBeNull();
    expect(host.querySelector('img')!.getAttribute('src')).toBe('/pictograms/brush-teeth.svg');
    expect(host.textContent).toContain('Brush teeth');
    expect(document.activeElement).toBe(trigger());
  });

  it('marks the chosen pictogram as pressed in the grid', async () => {
    const { fixture, trigger, tiles } = await setup('pictogram:shower');
    trigger().click();
    fixture.detectChanges();

    const pressed = tiles().filter((t) => t.getAttribute('aria-pressed') === 'true');
    expect(pressed.map((t) => t.textContent?.trim())).toEqual(['Shower']);
  });

  it('removes a chosen pictogram and brings the emoji input back', async () => {
    const { fixture, host, onValueChange } = await setup('pictogram:shower');

    host.querySelector<HTMLButtonElement>('button[aria-label="Remove picture"]')!.click();
    fixture.detectChanges();

    expect(onValueChange).toHaveBeenLastCalledWith('');
    expect(host.querySelector('input[name="stepIcon"]')).not.toBeNull();
  });

  it('closes on Escape, returning focus to the button, and on a backdrop click', async () => {
    const { fixture, host, trigger } = await setup();
    trigger().click();
    fixture.detectChanges();

    host.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(host.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(trigger());

    trigger().click();
    fixture.detectChanges();
    host.querySelector<HTMLElement>('div.fixed')!.click();
    fixture.detectChanges();

    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });

  it('closes when the button is pressed again', async () => {
    const { fixture, host, trigger } = await setup();
    trigger().click();
    fixture.detectChanges();
    trigger().click();
    fixture.detectChanges();

    expect(host.querySelector('[role="dialog"]')).toBeNull();
  });
});
