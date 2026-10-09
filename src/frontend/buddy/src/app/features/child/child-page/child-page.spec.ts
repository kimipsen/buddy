import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { ChildPage } from './child-page';

@Component({
  imports: [ChildPage],
  template: `
    <app-child-page [title]="'Calendar'" [backLabel]="backLabel()">
      <button childPageActions type="button">Next week</button>
      <p>Content</p>
    </app-child-page>
  `,
})
class Host {
  readonly backLabel = signal<string | undefined>(undefined);
}

describe('ChildPage', () => {
  async function setup(backLabel?: string) {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.backLabel.set(backLabel);
    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  }

  it('renders the title as the h1 inside a main landmark', async () => {
    const compiled = await setup();

    expect(compiled.querySelector('main header h1')?.textContent).toBe('Calendar');
  });

  it('shows the logo and no back link on the home page', async () => {
    const compiled = await setup();

    expect(compiled.querySelector('header a')).toBeNull();
    expect(compiled.querySelector('header span')?.textContent.trim()).toBe('B');
  });

  it('shows a back link to the child home instead of the logo when given a label', async () => {
    const compiled = await setup('Back');

    const link = compiled.querySelector('header a') as HTMLAnchorElement;
    expect(link.textContent.trim()).toBe('← Back');
    expect(link.getAttribute('href')).toBe('/child');
    expect(compiled.querySelector('header span')).toBeNull();
  });

  it('projects header actions into the header and the rest into the content column', async () => {
    const compiled = await setup();

    expect(compiled.querySelector('header button')?.textContent).toBe('Next week');
    expect(compiled.querySelector('main > section p')?.textContent).toBe('Content');
    expect(compiled.querySelector('main > section button')).toBeNull();
  });

  it('pads the header and the content column less on a phone', async () => {
    const compiled = await setup();

    for (const el of [compiled.querySelector('header'), compiled.querySelector('main > section')]) {
      expect([...(el as HTMLElement).classList]).toEqual(
        expect.arrayContaining(['px-4', 'sm:px-6', 'lg:px-8']),
      );
    }
    expect(compiled.querySelector('main > section')?.classList).toContain('max-w-3xl');
  });
});
