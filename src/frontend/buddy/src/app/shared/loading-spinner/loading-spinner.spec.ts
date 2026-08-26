import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { LoadingSpinner } from './loading-spinner';

describe('LoadingSpinner', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [LoadingSpinner] }).compileComponents();

    const fixture = TestBed.createComponent(LoadingSpinner);
    fixture.detectChanges();

    return { fixture, compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the app-loading-spinner element with a status role', async () => {
    const { compiled } = await setup();

    expect(compiled.querySelector('[role="status"]')).toBeTruthy();
  });

  it('renders no label text by default', async () => {
    const { compiled } = await setup();

    expect(compiled.textContent?.trim()).toBe('');
  });

  it('renders the label text when provided', async () => {
    const { fixture, compiled } = await setup();

    fixture.componentRef.setInput('label', 'Loading tasks…');
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Loading tasks…');
  });
});
