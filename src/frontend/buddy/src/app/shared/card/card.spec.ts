import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { Card } from './card';

@Component({
  imports: [Card],
  template: `
    <app-card class="mb-6" role="region" aria-labelledby="card-title">
      <h3 id="card-title">Children</h3>
      <p>Content</p>
    </app-card>
  `,
})
class Host {}

describe('Card', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();

    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    return { card: compiled.querySelector('app-card') as HTMLElement };
  }

  it('projects its content', async () => {
    const { card } = await setup();

    expect(card.querySelector('h3')?.textContent).toBe('Children');
    expect(card.querySelector('p')?.textContent).toBe('Content');
  });

  it('has phone padding that grows from sm up, in light and dark', async () => {
    const { card } = await setup();

    expect([...card.classList]).toEqual(
      expect.arrayContaining([
        'block',
        'rounded-lg',
        'border',
        'border-slate-200',
        'dark:border-slate-800',
        'bg-white',
        'dark:bg-slate-900',
        'p-4',
        'sm:p-6',
        'shadow-sm',
      ]),
    );
    expect(card.classList).not.toContain('p-6');
  });

  it("keeps the caller's own classes and attributes", async () => {
    const { card } = await setup();

    expect(card.classList).toContain('mb-6');
    expect(card.getAttribute('role')).toBe('region');
    expect(card.getAttribute('aria-labelledby')).toBe('card-title');
  });
});
