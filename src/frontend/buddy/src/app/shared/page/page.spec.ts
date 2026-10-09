import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { Page, PageWidth } from './page';

@Component({
  imports: [Page],
  template: `
    <app-page [width]="width()" [backLink]="backLink()" [backLabel]="'Back to dashboard'">
      <h2>Medicine</h2>
    </app-page>
  `,
})
class Host {
  readonly width = signal<PageWidth>('7xl');
  readonly backLink = signal<string | undefined>(undefined);
}

describe('Page', () => {
  async function setup(options: { width?: PageWidth; backLink?: string } = {}) {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(Host);
    if (options.width) {
      fixture.componentInstance.width.set(options.width);
    }
    fixture.componentInstance.backLink.set(options.backLink);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      compiled,
      section: compiled.querySelector('app-page > section') as HTMLElement,
    };
  }

  it('projects its content into a section', async () => {
    const { section } = await setup();

    expect(section.querySelector('h2')?.textContent).toBe('Medicine');
  });

  it('pads the page less on a phone than from sm and lg up', async () => {
    const { section } = await setup();

    expect([...section.classList]).toEqual(
      expect.arrayContaining(['mx-auto', 'px-4', 'py-8', 'sm:px-6', 'lg:px-8']),
    );
    expect(section.classList).not.toContain('px-6');
  });

  it('is capped at max-w-7xl by default', async () => {
    const { section } = await setup();

    expect(section.classList).toContain('max-w-7xl');
    expect(section.classList).not.toContain('max-w-3xl');
  });

  it.each<[PageWidth, string]>([
    ['3xl', 'max-w-3xl'],
    ['4xl', 'max-w-4xl'],
    ['5xl', 'max-w-5xl'],
  ])('uses the narrower cap for width %s', async (width, cls) => {
    const { section } = await setup({ width });

    expect(section.classList).toContain(cls);
    expect(section.classList).not.toContain('max-w-7xl');
  });

  it('swaps the cap when the width changes, keeping its padding', async () => {
    const { fixture, section } = await setup({ width: '3xl' });

    fixture.componentInstance.width.set('5xl');
    fixture.detectChanges();

    expect(section.classList).toContain('max-w-5xl');
    expect(section.classList).not.toContain('max-w-3xl');
    expect([...section.classList]).toEqual(
      expect.arrayContaining(['mx-auto', 'px-4', 'py-8', 'sm:px-6', 'lg:px-8']),
    );
  });

  it('shows no back link without a route', async () => {
    const { compiled } = await setup();

    expect(compiled.querySelector('a')).toBeNull();
  });

  it('shows the back link with its label and route', async () => {
    const { compiled } = await setup({ backLink: '/guardian' });

    const link = compiled.querySelector('a') as HTMLAnchorElement;
    expect(link.textContent.trim()).toBe('Back to dashboard');
    expect(link.getAttribute('href')).toBe('/guardian');
    expect(link.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });
});
