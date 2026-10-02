import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { CurrentUser, UsersService } from '../../../../core/users.service';
import { PrintTemplate, PrintTemplatesService } from '../../../../core/print-templates.service';
import { WeekPlanLoader } from '../week-plan-loader';
import { WeekPlanPrintPage } from './week-plan-print-page';

describe('WeekPlanPrintPage', () => {
  const template: PrintTemplate = {
    id: 't-1',
    ownerUserId: 'me',
    ownerGroupId: null,
    name: 'Ugeplan',
    paperSize: 1,
    defaultStartWeekday: 0,
    showWeekNumber: true,
    rows: [],
    guardianColors: [],
  };

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 1, 12)); // Thursday
  });

  afterEach(() => {
    vi.useRealTimers();
    document.head.querySelectorAll('style[data-week-plan-print]').forEach((s) => s.remove());
  });

  async function setup(start: string | null, get = vi.fn(async () => template)) {
    const load = vi.fn(async () => ({
      meals: new Map(),
      pickups: new Map(),
      workDays: new Map(),
      occurrences: new Map(),
      names: new Map(),
    }));

    await TestBed.configureTestingModule({
      imports: [WeekPlanPrintPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ templateId: 't-1' }),
              queryParamMap: convertToParamMap(start ? { start } : {}),
            },
          },
        },
        { provide: PrintTemplatesService, useValue: { get } },
        { provide: WeekPlanLoader, useValue: { load } },
        {
          provide: UsersService,
          useValue: {
            ensureCurrentUser: vi.fn(async () => ({ id: 'me' }) as CurrentUser),
            timeZoneId: () => 'Europe/Copenhagen',
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(WeekPlanPrintPage);
    await settle(fixture);
    return { fixture, load, root: fixture.nativeElement as HTMLElement };
  }

  it('injects exactly one @page style for the template’s paper and removes it on destroy', async () => {
    const { fixture } = await setup('2026-10-05');

    const styles = document.head.querySelectorAll('style[data-week-plan-print]');
    expect(styles).toHaveLength(1);
    expect(styles[0].textContent).toContain('@page { size: A3 landscape; margin: 8mm; }');

    fixture.destroy();
    expect(document.head.querySelectorAll('style[data-week-plan-print]')).toHaveLength(0);
  });

  it('adds no @page style when the page is left before the template arrives', async () => {
    let resolve!: (t: PrintTemplate) => void;
    const { fixture } = await setup(
      '2026-10-05',
      vi.fn(() => new Promise<PrintTemplate>((r) => (resolve = r))),
    );

    fixture.destroy();
    resolve(template);
    await new Promise((r) => setTimeout(r, 0));

    expect(document.head.querySelectorAll('style[data-week-plan-print]')).toHaveLength(0);
  });

  it('renders the sheet for the requested start date', async () => {
    const { load, root } = await setup('2026-10-05');

    expect(load).toHaveBeenCalledWith(template, '2026-10-05');
    expect(root.querySelector('app-week-plan-sheet')).toBeTruthy();
    expect(root.querySelector('[role="columnheader"]')?.textContent).toContain('Week 41');
  });

  it('falls back to the next default start weekday for a missing or malformed date', async () => {
    const { load } = await setup('next tuesday');

    // Default start weekday is Sunday; the next one on or after Thursday 2026-10-01.
    expect(load).toHaveBeenCalledWith(template, '2026-10-04');
  });

  it('reloads with a new date and keeps it in the URL', async () => {
    const { fixture, load, root } = await setup('2026-10-05');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const input = root.querySelector<HTMLInputElement>('app-date-select input')!;
    input.value = '2026-10-12';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(load).toHaveBeenLastCalledWith(template, '2026-10-12');
    expect(navigate).toHaveBeenCalledWith([], {
      queryParams: { start: '2026-10-12' },
      replaceUrl: true,
    });
  });

  it('focuses Print once the sheet is ready', async () => {
    const { root } = await setup('2026-10-05');

    expect(document.activeElement?.textContent?.trim()).toBe('Print');
    expect(root.contains(document.activeElement)).toBe(true);
  });

  it('does not pull focus back to Print when the date changes', async () => {
    const { fixture, root } = await setup('2026-10-05');
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const input = root.querySelector<HTMLInputElement>('app-date-select input')!;

    input.focus();
    input.value = '2026-10-12';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(document.activeElement).toBe(input);
  });

  it('prints through the browser', async () => {
    const { root } = await setup('2026-10-05');
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    Array.from(root.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Print')!
      .click();

    expect(print).toHaveBeenCalledOnce();
  });

  it('shows an error when the template can’t be loaded', async () => {
    const { root } = await setup(
      '2026-10-05',
      vi.fn(async () => {
        throw new Error('404');
      }),
    );

    expect(root.textContent).toContain('Unable to load this template.');
    expect(document.head.querySelectorAll('style[data-week-plan-print]')).toHaveLength(0);
  });
});
