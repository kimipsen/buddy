import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';

import { injectPrintPageStyle } from './print-page-style';

@Component({ template: '' })
class Host {
  readonly style = injectPrintPageStyle('test-print');
}

describe('injectPrintPageStyle', () => {
  afterEach(() => {
    document.documentElement.classList.remove('dark');
  });

  it('adds one @page style per page, updates it and removes it on destroy', () => {
    const fixture = TestBed.createComponent(Host);

    fixture.componentInstance.style.apply({ size: 'A4', orientation: 'portrait', margin: '12mm' });
    fixture.componentInstance.style.apply({ size: 'A3', orientation: 'landscape', margin: '8mm' });

    const styles = document.head.querySelectorAll('style[data-test-print]');
    expect(styles).toHaveLength(1);
    expect(styles[0].textContent).toContain('@page { size: A3 landscape; margin: 8mm; }');
    expect(styles[0].textContent).toContain('color-scheme: light');
    expect(styles[0].getAttribute('data-test-print')).toBe('');

    fixture.destroy();
    expect(document.head.querySelectorAll('style[data-test-print]')).toHaveLength(0);
  });

  it('takes the dark theme off for the print and puts it back afterwards', () => {
    document.documentElement.classList.add('dark');
    const fixture = TestBed.createComponent(Host);

    window.dispatchEvent(new Event('beforeprint'));
    expect(document.documentElement.classList.contains('dark')).toBe(false);

    window.dispatchEvent(new Event('afterprint'));
    expect(document.documentElement.classList.contains('dark')).toBe(true);

    fixture.destroy();
  });

  it('leaves a light theme light and stops listening once destroyed', () => {
    const fixture = TestBed.createComponent(Host);

    window.dispatchEvent(new Event('beforeprint'));
    window.dispatchEvent(new Event('afterprint'));
    expect(document.documentElement.classList.contains('dark')).toBe(false);

    fixture.destroy();
    document.documentElement.classList.add('dark');
    window.dispatchEvent(new Event('beforeprint'));
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('leaves a light theme light when the page goes away without a print', () => {
    const fixture = TestBed.createComponent(Host);

    fixture.destroy();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('puts the dark theme back when the page goes away mid-print', () => {
    document.documentElement.classList.add('dark');
    const fixture = TestBed.createComponent(Host);

    window.dispatchEvent(new Event('beforeprint'));
    fixture.destroy();

    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('forgets the theme once a print is over', () => {
    document.documentElement.classList.add('dark');
    const fixture = TestBed.createComponent(Host);

    window.dispatchEvent(new Event('beforeprint'));
    window.dispatchEvent(new Event('afterprint'));
    // The guardian switches to the light theme after printing.
    document.documentElement.classList.remove('dark');
    fixture.destroy();

    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });
});
