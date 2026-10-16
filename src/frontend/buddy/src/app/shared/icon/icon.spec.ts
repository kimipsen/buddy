import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { Icon } from './icon';

describe('Icon', () => {
  async function setup(inputs: {
    value?: string | null;
    size?: 'inline' | 'tile';
    label?: string;
  }) {
    await TestBed.configureTestingModule({ imports: [Icon] }).compileComponents();

    const fixture = TestBed.createComponent(Icon);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();

    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('renders an emoji as decorative text', async () => {
    const { host } = await setup({ value: '🪥' });

    const span = host.querySelector('span')!;
    expect(span.textContent).toBe('🪥');
    expect(span.getAttribute('aria-hidden')).toBe('true');
    expect(host.querySelector('img')).toBeNull();
  });

  it('renders a pictogram as a decorative image of the inline size', async () => {
    const { host } = await setup({ value: 'pictogram:brush-teeth' });

    const img = host.querySelector('img')!;
    expect(img.getAttribute('src')).toBe('/pictograms/brush-teeth.svg');
    expect(img.getAttribute('alt')).toBe('');
    expect(img.getAttribute('aria-hidden')).toBe('true');
    expect(img.className).toBe('h-[1.25em] w-[1.25em]');
    expect(host.querySelector('span')).toBeNull();
  });

  it('renders a tile-sized pictogram at 48px', async () => {
    const { host } = await setup({ value: 'pictogram:brush-teeth', size: 'tile' });

    expect(host.querySelector('img')!.className).toBe('h-12 w-12');
  });

  it('names a labelled pictogram for screen readers', async () => {
    const { host } = await setup({ value: 'pictogram:homework', label: 'Homework' });

    const img = host.querySelector('img')!;
    expect(img.getAttribute('alt')).toBe('Homework');
    expect(img.hasAttribute('aria-hidden')).toBe(false);
  });

  it('names a labelled emoji for screen readers', async () => {
    const { host } = await setup({ value: '📚', label: 'Homework' });

    const span = host.querySelector('span')!;
    expect(span.getAttribute('role')).toBe('img');
    expect(span.getAttribute('aria-label')).toBe('Homework');
    expect(span.hasAttribute('aria-hidden')).toBe(false);
  });

  it('renders nothing for no icon', async () => {
    const { host } = await setup({ value: '' });

    expect(host.children.length).toBe(0);
  });

  it('falls back to a picture emoji when the pictogram cannot be loaded, until the value changes', async () => {
    const { fixture, host } = await setup({ value: 'pictogram:unicorn' });

    host.querySelector('img')!.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(host.querySelector('img')).toBeNull();
    expect(host.querySelector('span')!.textContent).toBe('🖼️');

    fixture.componentRef.setInput('value', 'pictogram:bath');
    fixture.detectChanges();

    expect(host.querySelector('img')!.getAttribute('src')).toBe('/pictograms/bath.svg');
  });
});
