import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { VisitSummariesService, VisitSummary } from '../../../../core/visit-summaries.service';
import { VisitSummaryShareLinks } from './visit-summary-share-links';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(compiled.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  );
}

const summary: VisitSummary = {
  id: 'summary-1',
  childId: 'child-1',
  title: 'Follow-up',
  visitDate: null,
  from: '2026-03-02',
  to: '2026-03-29',
  sections: ['Sleep'],
  note: '',
  createdBy: 'guardian-1',
  lastModifiedAt: '2026-03-30T10:00:00Z',
  shareLinks: [
    { id: 'link-1', createdAt: '2026-03-01T10:00:00Z', expiresAt: '2026-03-31T10:00:00Z' },
    { id: 'link-2', createdAt: '2026-03-02T10:00:00Z', expiresAt: null },
  ],
};

describe('VisitSummaryShareLinks', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(service: Partial<VisitSummariesService> = {}, value = summary) {
    const stub: Partial<VisitSummariesService> = {
      createShareLink: vi.fn(async () => ({
        id: 'link-3',
        token: 'secret-token',
        createdAt: '2026-03-05T10:00:00Z',
        expiresAt: null,
      })),
      revokeShareLink: vi.fn(async () => undefined),
      ...service,
    };

    await TestBed.configureTestingModule({
      imports: [VisitSummaryShareLinks],
      providers: [{ provide: VisitSummariesService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(VisitSummaryShareLinks);
    fixture.componentRef.setInput('summary', value);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, stub, changed };
  }

  it('lists the summary’s live links with their expiry', async () => {
    const { compiled } = await setup();

    const items = Array.from(compiled.querySelectorAll('li')).map((li) => li.textContent);
    expect(items[0]).toContain('expires Mar 31, 2026');
    expect(items[1]).toContain('never expires');
  });

  it('says so when there are no links', async () => {
    const { compiled } = await setup({}, { ...summary, shareLinks: [] });

    expect(compiled.textContent).toContain('No active links.');
  });

  it('creates a 30-day link by default, shows it once and asks for a reload', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-03-05T10:00:00Z'));
    const { fixture, compiled, stub, changed } = await setup();

    button(compiled, 'Create link')!.click();
    await settle(fixture);

    expect(stub.createShareLink).toHaveBeenCalledWith(
      'child-1',
      'summary-1',
      '2026-04-04T10:00:00.000Z',
    );
    expect(compiled.textContent).toContain(
      `${window.location.origin}/shared/visit-summary/secret-token`,
    );
    expect(changed).toHaveBeenCalledTimes(1);
  });

  it('creates a link with no expiry when Never is chosen', async () => {
    const { fixture, compiled, stub } = await setup();

    button(compiled, 'Never')!.click();
    await settle(fixture);
    button(compiled, 'Create link')!.click();
    await settle(fixture);

    expect(stub.createShareLink).toHaveBeenCalledWith('child-1', 'summary-1', null);
  });

  it('shows the limit message when the link can’t be created', async () => {
    const { fixture, compiled, changed } = await setup({
      createShareLink: vi.fn(async () => {
        throw new Error('400');
      }),
    });

    button(compiled, 'Create link')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('A summary can have at most 5 active links.');
    expect(changed).not.toHaveBeenCalled();
  });

  it('revokes a link and asks for a reload, or shows the error', async () => {
    const { fixture, compiled, stub, changed } = await setup();

    button(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(stub.revokeShareLink).toHaveBeenCalledWith('child-1', 'summary-1', 'link-1');
    expect(changed).toHaveBeenCalledTimes(1);

    vi.mocked(stub.revokeShareLink!).mockRejectedValueOnce(new Error('500'));
    button(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Could not revoke the link.');
  });
});
