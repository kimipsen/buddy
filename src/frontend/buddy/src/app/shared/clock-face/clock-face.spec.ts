import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { en } from '../../core/i18n/translations/en';
import { da } from '../../core/i18n/translations/da';
import { ClockFace } from './clock-face';

describe('ClockFace', () => {
  async function setup(hour: number, minute: number, ariaLabel?: string) {
    await TestBed.configureTestingModule({ imports: [ClockFace] }).compileComponents();
    const fixture = TestBed.createComponent(ClockFace);
    fixture.componentRef.setInput('hour', hour);
    fixture.componentRef.setInput('minute', minute);
    if (ariaLabel !== undefined) {
      fixture.componentRef.setInput('ariaLabel', ariaLabel);
    }
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it.each([
    [3, 0, 90, 0],
    [3, 30, 105, 180],
    [12, 0, 0, 0],
    [6, 45, 202.5, 270],
    [12, 30, 15, 180],
    [11, 55, 357.5, 330],
  ])('%i:%i puts the hands at %f and %f degrees', async (hour, minute, hourAngle, minuteAngle) => {
    const { fixture } = await setup(hour, minute);
    expect(fixture.componentInstance.hourAngle()).toBeCloseTo(hourAngle, 5);
    expect(fixture.componentInstance.minuteAngle()).toBeCloseTo(minuteAngle, 5);
  });

  function end(el: HTMLElement, part: string): { x: number; y: number } {
    const line = el.querySelector(`[data-part="${part}"]`)!;
    return { x: Number(line.getAttribute('x2')), y: Number(line.getAttribute('y2')) };
  }

  it('draws the hands from the centre towards the right angle', async () => {
    const { el } = await setup(3, 0);
    const hour = end(el, 'hour-hand');
    const minute = end(el, 'minute-hand');
    expect(hour.x).toBeCloseTo(72, 5);
    expect(hour.y).toBeCloseTo(50, 5);
    expect(minute.x).toBeCloseTo(50, 5);
    expect(minute.y).toBeCloseTo(17, 5);
  });

  it('draws twelve ticks and the numerals 12, 3, 6 and 9', async () => {
    const { el } = await setup(3, 0);
    expect(el.querySelectorAll('[data-part="tick"]').length).toBe(12);
    const numerals = Array.from(el.querySelectorAll('[data-part="numeral"]')).map((n) =>
      n.textContent?.trim(),
    );
    expect(numerals).toEqual(['12', '3', '6', '9']);
  });

  it('exposes the label as an image with an aria-label', async () => {
    const { el } = await setup(3, 0, 'A clock');
    const svg = el.querySelector('svg')!;
    expect(svg.getAttribute('role')).toBe('img');
    expect(svg.getAttribute('aria-label')).toBe('A clock');
  });

  it('omits the aria-label when none is given', async () => {
    const { el } = await setup(3, 0);
    expect(el.querySelector('svg')!.hasAttribute('aria-label')).toBe(false);
  });

  it('uses theme token classes with a dark pair on every part and no hard-coded colour', async () => {
    const { el } = await setup(3, 0);
    const parts = Array.from(el.querySelectorAll('[data-part]'));
    expect(parts.length).toBeGreaterThan(0);
    for (const part of parts) {
      const cls = part.getAttribute('class') ?? '';
      expect(cls).toMatch(/dark:(fill|stroke)-/);
      expect(part.hasAttribute('fill')).toBe(false);
      expect(part.hasAttribute('stroke')).toBe(false);
      expect(part.getAttribute('style')).toBeNull();
    }
  });

  it('has an aria-label template in both languages naming both hand positions', () => {
    for (const dict of [en, da]) {
      const template = dict.flashCards.clock.ariaLabel;
      expect(template).toContain('{hour}');
      expect(template).toContain('{minute}');
    }
  });
});
