import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { GuardiansService } from '../../../core/guardians.service';
import { VisitSummariesService, VisitSummary } from '../../../core/visit-summaries.service';
import { GuardianVisitSummaries } from './visit-summaries';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function summary(id: string, overrides: Partial<VisitSummary> = {}): VisitSummary {
  return {
    id,
    childId: 'child-1',
    title: `Summary ${id}`,
    visitDate: '2026-04-02',
    from: '2026-03-02',
    to: '2026-03-29',
    sections: ['Medicine', 'Sleep'],
    note: '',
    createdBy: 'guardian-1',
    lastModifiedAt: '2026-03-30T10:00:00Z',
    shareLinks: [],
    ...overrides,
  };
}

const children = [
  { id: 'child-1', name: { givenName: 'Emil', familyName: 'Holm' } },
  { id: 'child-2', name: { givenName: 'Ida', familyName: 'Holm' } },
];

describe('GuardianVisitSummaries', () => {
  async function setup(list: VisitSummariesService['list'], kids: unknown[] = children) {
    await TestBed.configureTestingModule({
      imports: [GuardianVisitSummaries],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: { listMyChildren: vi.fn(async () => kids) } },
        {
          provide: VisitSummariesService,
          useValue: { list, create: vi.fn(async () => summary('new')) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(GuardianVisitSummaries);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('lists the first child’s summaries with their period, sections and links', async () => {
    const list = vi.fn(async () => [
      summary('a', { shareLinks: [{ id: 'l', createdAt: '', expiresAt: null }] }),
      summary('b', { visitDate: null }),
    ]);
    const { el } = await setup(list);

    expect(list).toHaveBeenCalledWith('child-1');
    const cards = Array.from(el.querySelectorAll('li a'));
    expect(cards).toHaveLength(2);
    expect(cards[0].textContent).toContain('Summary a');
    expect(cards[0].textContent).toContain('Mar 2, 2026 – Mar 29, 2026');
    expect(cards[0].textContent).toContain('Visit Apr 2, 2026');
    expect(cards[0].textContent).toContain('Medicine');
    expect(cards[0].textContent).toContain('1 active links');
    expect(cards[0].getAttribute('href')).toBe('/guardian/visit-summaries/child-1/a');
    expect(cards[1].textContent).not.toContain('Visit');
  });

  it('switches child', async () => {
    const list = vi.fn(async () => []);
    const { fixture, el } = await setup(list);

    const select = el.querySelector('select')!;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(list).toHaveBeenLastCalledWith('child-2');
    expect(el.textContent).toContain('No visit summaries yet.');
  });

  it('opens the editor and goes to a new summary once it is saved', async () => {
    const { fixture, el } = await setup(vi.fn(async () => []));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'New summary')!
      .click();
    await settle(fixture);
    expect(el.textContent).toContain('New visit summary');

    const title = el.querySelector<HTMLInputElement>('input[type="text"]')!;
    title.value = 'Follow-up';
    title.dispatchEvent(new Event('input'));
    Array.from(el.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Save summary')!
      .click();
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith(['/guardian/visit-summaries', 'child-1', 'new']);
  });

  it('says so when there is no child, or the list fails', async () => {
    const empty = await setup(
      vi.fn(async () => []),
      [],
    );
    expect(empty.el.textContent).toContain('Link a child under Settings');

    TestBed.resetTestingModule();
    const failed = await setup(
      vi.fn(async () => {
        throw new Error('500');
      }),
    );
    expect(failed.el.textContent).toContain('Could not load the visit summaries.');
  });
});
