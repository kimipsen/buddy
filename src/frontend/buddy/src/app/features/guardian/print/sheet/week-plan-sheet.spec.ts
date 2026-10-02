import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { WeekPlanModel } from '../week-plan-model';
import { WeekPlanSheet } from './week-plan-sheet';

describe('WeekPlanSheet', () => {
  const days = Array.from({ length: 7 }, (_, i) => ({
    date: `2026-09-${28 + i}`,
    weekday: `Day ${i}`,
    dayLabel: `${28 + i} Sep`,
  }));

  function model(overrides: Partial<WeekPlanModel> = {}): WeekPlanModel {
    return {
      paperSize: 0,
      weekLabel: 'Week 40',
      days,
      rows: [
        {
          kind: 1,
          label: 'Aflevere / Hente',
          heightWeight: 1,
          unavailable: false,
          cells: [
            {
              type: 'pickup',
              dropOff: { text: 'Far', icon: null, color: '#2563eb' },
              pickUp: { text: 'Mor', icon: null, color: null },
            },
            ...Array(6).fill({ type: 'blank' }),
          ],
        },
        {
          kind: 4,
          label: 'Signes aktiviteter',
          heightWeight: 3,
          unavailable: false,
          cells: [
            {
              type: 'list',
              items: [{ time: '16:00', text: 'Kor', assignee: 'Signe' }],
              overflow: 2,
            },
            { type: 'checklist', items: ['Affald + pant'], overflow: 0 },
            { type: 'mark' },
            { type: 'text', value: { text: 'Stil', icon: '🏢', color: '#0ea5e9' } },
            ...Array(3).fill({ type: 'blank' }),
          ],
        },
        {
          kind: 3,
          label: 'Privat',
          heightWeight: 1,
          unavailable: true,
          cells: Array(7).fill({ type: 'blank' }),
        },
      ],
      ...overrides,
    };
  }

  async function render(value: WeekPlanModel): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [WeekPlanSheet] }).compileComponents();
    const fixture = TestBed.createComponent(WeekPlanSheet);
    fixture.componentRef.setInput('model', value);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders a header row and one row per template row, sized by height weight', async () => {
    const root = await render(model());
    const sheet = root.querySelector<HTMLElement>('[role="table"]')!;

    expect(sheet.getAttribute('aria-label')).toBe('Week 40');
    expect(root.querySelectorAll('[role="columnheader"]')).toHaveLength(8);
    const headers = Array.from(root.querySelectorAll('[role="rowheader"]'));
    expect(headers.map((h) => h.querySelector('span')?.textContent?.trim())).toEqual([
      'Aflevere / Hente',
      'Signes aktiviteter',
      'Privat',
    ]);
    // Only the row whose source failed carries the note.
    expect(headers.map((h) => h.textContent?.includes('Not available'))).toEqual([
      false,
      false,
      true,
    ]);
    expect(sheet.style.gridTemplateRows).toBe('auto 1fr 3fr 1fr');
  });

  it('sizes A4 in millimetres and scales the font up for A3', async () => {
    const a4 = (await render(model())).querySelector<HTMLElement>('[role="table"]')!;
    expect(a4.style.width).toBe('281mm');
    expect(a4.style.height).toBe('194mm');
    expect(a4.style.fontSize).toBe('9pt');
    TestBed.resetTestingModule();

    const a3 = (await render(model({ paperSize: 1 }))).querySelector<HTMLElement>(
      '[role="table"]',
    )!;
    expect(a3.style.width).toBe('404mm');
    expect(parseFloat(a3.style.fontSize)).toBeCloseTo(9 * Math.SQRT2, 3);
  });

  it('renders each cell kind', async () => {
    const root = await render(model());
    const cells = root.querySelectorAll('[role="cell"]');

    expect(cells[0].textContent).toContain('Far');
    expect(cells[0].textContent).toContain('Mor');
    expect(cells[0].querySelector('svg line')).toBeTruthy();
    expect(cells[7].textContent).toContain('16:00');
    expect(cells[7].textContent).toContain('Kor');
    expect(cells[7].textContent).toContain('(Signe)');
    expect(cells[7].textContent).toContain('+2');
    expect(cells[8].textContent).toContain('Affald + pant');
    expect(cells[9].textContent).toContain('✕');
    expect(cells[9].querySelector('.sr-only')?.textContent).toBe('Yes');
    expect(cells[10].textContent).toContain('Stil');
  });

  it('tells screen readers which pickup name is the drop-off and which the pick-up', async () => {
    const root = await render(model());
    const labels = Array.from(
      root.querySelectorAll('[role="cell"]')[0].querySelectorAll('.sr-only'),
    ).map((s) => s.textContent?.trim());

    expect(labels).toEqual(['Drop-off:', 'Pick-up:']);
  });

  it('leaves the week corner empty when the week number is hidden', async () => {
    const root = await render(model({ weekLabel: null }));

    expect(root.querySelector('[role="columnheader"]')?.textContent?.trim()).toBe('');
    expect(root.querySelector('[role="table"]')?.getAttribute('aria-label')).toBe('Week plan');
  });
});
