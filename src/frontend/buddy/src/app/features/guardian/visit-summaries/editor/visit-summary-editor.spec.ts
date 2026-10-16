import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { VisitSummariesService, VisitSummary } from '../../../../core/visit-summaries.service';
import { VisitSummaryEditor } from './visit-summary-editor';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(compiled: HTMLElement, text: string): HTMLButtonElement {
  const found = Array.from(compiled.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  );
  if (!found) {
    throw new Error(`No button "${text}"`);
  }
  return found;
}

function type(element: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  element.value = value;
  element.dispatchEvent(new Event('input'));
}

const existing: VisitSummary = {
  id: 'summary-1',
  childId: 'child-1',
  title: 'March follow-up',
  visitDate: '2026-04-02',
  from: '2026-03-02',
  to: '2026-03-29',
  sections: ['Sleep'],
  note: 'Ask about lunch',
  createdBy: 'guardian-1',
  lastModifiedAt: '2026-03-30T10:00:00Z',
  shareLinks: [],
};

describe('VisitSummaryEditor', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(summary: VisitSummary | null = null) {
    const stub: Partial<VisitSummariesService> = {
      create: vi.fn(
        async (_childId, request) => ({ ...existing, ...request, id: 'new' }) as VisitSummary,
      ),
      update: vi.fn(
        async (_childId, _id, request) => ({ ...existing, ...request }) as VisitSummary,
      ),
    };

    await TestBed.configureTestingModule({
      imports: [VisitSummaryEditor],
      providers: [{ provide: VisitSummariesService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(VisitSummaryEditor);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('summary', summary);
    const saved = vi.fn();
    const cancelled = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.componentInstance.cancelled.subscribe(cancelled);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, stub, saved, cancelled };
  }

  it('creates a summary for the last 4 weeks with every section by default', async () => {
    const { fixture, compiled, stub, saved } = await setup();

    type(compiled.querySelector('input[type="text"]')!, '  Follow-up  ');
    type(compiled.querySelector('textarea')!, ' Ask about lunch ');
    button(compiled, 'Save summary').click();
    await settle(fixture);

    expect(stub.create).toHaveBeenCalledWith('child-1', {
      title: 'Follow-up',
      visitDate: null,
      from: '2026-09-19',
      to: '2026-10-16',
      sections: ['Medicine', 'Sleep', 'Feelings'],
      note: 'Ask about lunch',
    });
    expect(saved).toHaveBeenCalledWith(expect.objectContaining({ id: 'new' }));
  });

  it('switches the period with the presets', async () => {
    const { fixture, compiled, stub } = await setup();

    type(compiled.querySelector('input[type="text"]')!, 'Follow-up');
    button(compiled, 'Last 3 months').click();
    await settle(fixture);
    button(compiled, 'Save summary').click();
    await settle(fixture);

    expect(stub.create).toHaveBeenCalledWith(
      'child-1',
      expect.objectContaining({ from: '2026-07-18', to: '2026-10-16' }),
    );
  });

  it('edits an existing summary, sending every detail and the toggled sections', async () => {
    const { fixture, compiled, stub } = await setup(existing);

    expect((compiled.querySelector('input[type="text"]') as HTMLInputElement).value).toBe(
      'March follow-up',
    );
    const toggles = compiled.querySelectorAll<HTMLButtonElement>('button[role="switch"]');
    expect([...toggles].map((t) => t.getAttribute('aria-checked'))).toEqual([
      'false',
      'true',
      'false',
    ]);

    toggles[2].click();
    await settle(fixture);
    button(compiled, 'Save summary').click();
    await settle(fixture);

    expect(stub.update).toHaveBeenCalledWith('child-1', 'summary-1', {
      title: 'March follow-up',
      visitDate: '2026-04-02',
      from: '2026-03-02',
      to: '2026-03-29',
      sections: ['Sleep', 'Feelings'],
      note: 'Ask about lunch',
    });
  });

  it('checks the title, the sections and the period before saving', async () => {
    const { fixture, compiled, stub } = await setup(existing);

    type(compiled.querySelector('input[type="text"]')!, '   ');
    button(compiled, 'Save summary').click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Give the summary a title.');

    type(compiled.querySelector('input[type="text"]')!, 'Visit');
    compiled.querySelectorAll<HTMLButtonElement>('button[role="switch"]')[1].click();
    await settle(fixture);
    button(compiled, 'Save summary').click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Choose at least one section.');

    compiled.querySelectorAll<HTMLButtonElement>('button[role="switch"]')[0].click();
    const [, from] = compiled.querySelectorAll<HTMLInputElement>('input[type="date"]');
    type(from, '2026-04-01');
    await settle(fixture);
    button(compiled, 'Save summary').click();
    await settle(fixture);
    expect(compiled.textContent).toContain('The period must end after it starts');

    expect(stub.update).not.toHaveBeenCalled();
  });

  it('shows an error when saving fails, and cancels', async () => {
    const { fixture, compiled, stub, saved, cancelled } = await setup(existing);
    vi.mocked(stub.update!).mockRejectedValueOnce(new Error('500'));

    button(compiled, 'Save summary').click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Could not save the summary.');
    expect(saved).not.toHaveBeenCalled();

    button(compiled, 'Cancel').click();
    expect(cancelled).toHaveBeenCalled();
  });
});
