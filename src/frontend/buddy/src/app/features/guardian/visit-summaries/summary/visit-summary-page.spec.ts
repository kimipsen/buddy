import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import {
  VisitSummariesService,
  VisitSummaryWithReport,
} from '../../../../core/visit-summaries.service';
import { VisitSummaryPage } from './visit-summary-page';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(el: HTMLElement, text: string): HTMLButtonElement {
  const found = Array.from(el.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  );
  if (!found) {
    throw new Error(`No button "${text}"`);
  }
  return found;
}

const loaded: VisitSummaryWithReport = {
  summary: {
    id: 'summary-1',
    childId: 'child-1',
    title: 'Follow-up',
    visitDate: null,
    from: '2026-03-02',
    to: '2026-03-08',
    sections: ['Sleep'],
    note: '',
    createdBy: 'guardian-1',
    lastModifiedAt: '2026-03-30T10:00:00Z',
    shareLinks: [],
  },
  report: {
    childGivenName: 'Emil',
    childFamilyName: 'Holm',
    title: 'Follow-up',
    visitDate: null,
    from: '2026-03-02',
    to: '2026-03-08',
    note: '',
    preparedBy: 'Sara Holm',
    sleep: { sleepHygieneNotes: '', entries: [] },
    medicine: null,
    feelings: null,
  },
};

describe('VisitSummaryPage', () => {
  async function setup(service: Partial<VisitSummariesService>) {
    await TestBed.configureTestingModule({
      imports: [VisitSummaryPage],
      providers: [
        provideRouter([]),
        { provide: VisitSummariesService, useValue: service },
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

    const fixture = TestBed.createComponent(VisitSummaryPage);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the report, the share links and a link to the printout', async () => {
    const get = vi.fn(async () => loaded);
    const { el } = await setup({ get });

    expect(get).toHaveBeenCalledWith('child-1', 'summary-1');
    expect(el.querySelector('h1')?.textContent).toContain('Emil Holm: Follow-up');
    expect(el.textContent).toContain('Share with the doctor');
    const print = Array.from(el.querySelectorAll('a')).find(
      (a) => a.textContent?.trim() === 'Print',
    );
    expect(print?.getAttribute('href')).toBe('/guardian/visit-summaries/print/child-1/summary-1');
  });

  it('edits the summary and reloads it once saved', async () => {
    const get = vi.fn(async () => loaded);
    const update = vi.fn(async () => loaded.summary);
    const { fixture, el } = await setup({ get, update });

    button(el, 'Edit').click();
    await settle(fixture);
    expect(el.textContent).toContain('Edit visit summary');

    button(el, 'Save summary').click();
    await settle(fixture);

    expect(update).toHaveBeenCalledWith(
      'child-1',
      'summary-1',
      expect.objectContaining({ title: 'Follow-up' }),
    );
    expect(get).toHaveBeenCalledTimes(2);
    expect(el.textContent).not.toContain('Edit visit summary');
  });

  it('deletes after confirming and goes back to the list', async () => {
    const deleteSummary = vi.fn(async () => undefined);
    const { fixture, el } = await setup({ get: vi.fn(async () => loaded), delete: deleteSummary });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    button(el, 'Delete summary').click();
    await settle(fixture);
    button(el, 'Keep').click();
    await settle(fixture);
    expect(deleteSummary).not.toHaveBeenCalled();

    button(el, 'Delete summary').click();
    await settle(fixture);
    button(el, 'Delete').click();
    await settle(fixture);

    expect(deleteSummary).toHaveBeenCalledWith('child-1', 'summary-1');
    expect(navigate).toHaveBeenCalledWith(['/guardian/visit-summaries']);
  });

  it('shows a delete error', async () => {
    const { fixture, el } = await setup({
      get: vi.fn(async () => loaded),
      delete: vi.fn(async () => {
        throw new Error('500');
      }),
    });

    button(el, 'Delete summary').click();
    await settle(fixture);
    button(el, 'Delete').click();
    await settle(fixture);

    expect(el.textContent).toContain('Could not delete the summary.');
  });

  it('tells a missing summary from a failed load', async () => {
    const missing = await setup({
      get: vi.fn(async () => {
        throw new HttpErrorResponse({ status: 404 });
      }),
    });
    expect(missing.el.textContent).toContain('This summary no longer exists.');

    TestBed.resetTestingModule();
    const failed = await setup({
      get: vi.fn(async () => {
        throw new HttpErrorResponse({ status: 500 });
      }),
    });
    expect(failed.el.textContent).toContain('Could not load the visit summaries.');
  });
});
