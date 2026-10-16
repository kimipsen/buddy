import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  SharedVisitSummary as SharedView,
  VisitSummariesService,
} from '../../core/visit-summaries.service';
import { SharedVisitSummary } from './shared-visit-summary';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const view: SharedView = {
  expiresAt: '2026-11-12T00:00:00Z',
  report: {
    childGivenName: 'Emil',
    childFamilyName: 'Holm',
    title: 'Follow-up',
    visitDate: null,
    from: '2026-03-02',
    to: '2026-03-08',
    note: 'Ask about lunch',
    preparedBy: 'Sara Holm',
    sleep: { sleepHygieneNotes: '', entries: [] },
    medicine: null,
    feelings: null,
  },
};

describe('SharedVisitSummary', () => {
  afterEach(() => {
    document.head.querySelectorAll('style[data-visit-summary-print]').forEach((s) => s.remove());
  });

  async function setup(getShared: VisitSummariesService['getShared']) {
    await TestBed.configureTestingModule({
      imports: [SharedVisitSummary],
      providers: [
        { provide: VisitSummariesService, useValue: { getShared } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ token: 'tok' }) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(SharedVisitSummary);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the report, the link’s expiry and an A4 portrait page style', async () => {
    const getShared = vi.fn(async () => view);
    const { el } = await setup(getShared);

    expect(getShared).toHaveBeenCalledWith('tok');
    expect(el.querySelector('h1')?.textContent).toContain('Emil Holm: Follow-up');
    expect(el.textContent).toContain('This link expires Nov 12, 2026.');
    expect(document.head.querySelector('style[data-visit-summary-print]')?.textContent).toContain(
      '@page { size: A4 portrait; margin: 12mm; }',
    );
  });

  it('prints from the print button', async () => {
    const { el } = await setup(vi.fn(async () => ({ ...view, expiresAt: null })));
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);

    expect(el.textContent).toContain('This link does not expire.');
    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Print')!
      .click();

    expect(print).toHaveBeenCalled();
    print.mockRestore();
  });

  it('explains a link that no longer works', async () => {
    const { el } = await setup(
      vi.fn(async () => {
        throw new HttpErrorResponse({ status: 404 });
      }),
    );

    expect(el.textContent).toContain('This link doesn’t work any more');
  });

  it('shows a general error for anything else', async () => {
    const { el } = await setup(
      vi.fn(async () => {
        throw new HttpErrorResponse({ status: 500 });
      }),
    );

    expect(el.textContent).toContain('Could not load the visit summary.');
  });
});
