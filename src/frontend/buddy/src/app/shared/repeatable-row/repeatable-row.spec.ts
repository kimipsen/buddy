import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { RepeatableRow } from './repeatable-row';

@Component({
  imports: [RepeatableRow],
  template: `<app-repeatable-row [canRemove]="canRemove" removeLabel="Remove" (remove)="onRemove()"><span>content</span></app-repeatable-row>`
})
class Host {
  canRemove = true;
  onRemove = vi.fn();
}

describe('RepeatableRow', () => {
  async function setup(canRemove = true) {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();

    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.canRemove = canRemove;
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement };
  }

  it('projects the row content', async () => {
    const { compiled } = await setup();

    expect(compiled.textContent).toContain('content');
  });

  it('renders the remove button with the given label when removable', async () => {
    const { compiled } = await setup(true);

    const button = compiled.querySelector('button');
    expect(button?.textContent?.trim()).toBe('Remove');
  });

  it('hides the remove button when not removable', async () => {
    const { compiled } = await setup(false);

    expect(compiled.querySelector('button')).toBeNull();
  });

  it('calls remove when the button is clicked', async () => {
    const { fixture, compiled } = await setup(true);

    compiled.querySelector('button')!.dispatchEvent(new Event('click'));

    expect(fixture.componentInstance.onRemove).toHaveBeenCalledTimes(1);
  });
});
