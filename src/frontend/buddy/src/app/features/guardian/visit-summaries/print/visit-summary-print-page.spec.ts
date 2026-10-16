import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  VisitSummariesService,
  VisitSummaryWithReport,
} from '../../../../core/visit-summaries.service';
import { VisitSummaryPrintPage } from './visit-summary-print-page';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const loaded = {
  summary: { id: 'summary-1' },
  report: {
    childGivenName: 'Emil',
    childFamilyName: 'Holm',
    title: 'Follow-up',
    visitDate: null,
    from: '2026-03-02',
    to: '2026-03-08',
    note: '',
    preparedBy: '',
    sleep: null,
    medicine: null,
    feelings: null,
  },
} as unknown as VisitSummaryWithReport;

describe('VisitSummaryPrintPage', () => {
  afterEach(() => {
    document.head.querySelectorAll('style[data-visit-summary-print]').forEach((s) => s.remove());
  });

  async function setup(get: VisitSummariesService['get']) {
    await TestBed.configureTestingModule({
      imports: [VisitSummaryPrintPage],
      providers: [
        provideRouter([]),
        { provide: VisitSummariesService, useValue: { get } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ childId: 'child-1', summaryId: 'summary-1' }),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(VisitSummaryPrintPage);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('renders the report on A4 portrait and prints it', async () => {
    const get = vi.fn(async () => loaded);
    const { fixture, el } = await setup(get);
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    expect(get).toHaveBeenCalledWith('child-1', 'summary-1');
    expect(el.querySelector('h1')?.textContent).toContain('Emil Holm: Follow-up');
    expect(document.head.querySelector('style[data-visit-summary-print]')?.textContent).toContain(
      '@page { size: A4 portrait; margin: 12mm; }',
    );

    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Print')!
      .click();
    expect(print).toHaveBeenCalled();
    print.mockRestore();

    fixture.destroy();
    expect(document.head.querySelector('style[data-visit-summary-print]')).toBeNull();
  });

  it('shows an error and no page style when the summary can’t be loaded', async () => {
    const { el } = await setup(
      vi.fn(async () => {
        throw new Error('500');
      }),
    );

    expect(el.textContent).toContain('Could not load the visit summaries.');
    expect(document.head.querySelector('style[data-visit-summary-print]')).toBeNull();
  });
});
