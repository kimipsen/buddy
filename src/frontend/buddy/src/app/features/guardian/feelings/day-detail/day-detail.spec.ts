import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { FeelingCheckIn, FeelingsService } from '../../../../core/feelings.service';
import { FeelingsDayDetail } from './day-detail';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}

function checkIn(overrides: Partial<FeelingCheckIn>): FeelingCheckIn {
  return {
    id: 'c1',
    zone: 'Green',
    tags: [],
    note: '',
    date: '2026-10-13',
    time: '08:00',
    loggedBy: 'child-1',
    isSelfReport: true,
    recordedAt: '2026-10-13T06:00:00Z',
    ...overrides,
  };
}

describe('FeelingsDayDetail', () => {
  async function setup(removeCheckIn = vi.fn(async () => undefined)) {
    await TestBed.configureTestingModule({
      imports: [FeelingsDayDetail],
      providers: [{ provide: FeelingsService, useValue: { removeCheckIn } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(FeelingsDayDetail);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('date', '2026-10-13');
    fixture.componentRef.setInput('checkIns', [
      checkIn({ id: 'own', tags: ['Happy'], note: 'Football' }),
      checkIn({ id: 'seen', zone: 'Red', isSelfReport: false, time: '15:10' }),
      checkIn({ id: 'other-day', date: '2026-10-12' }),
    ]);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement, removeCheckIn };
  }

  function items(el: HTMLElement): HTMLElement[] {
    return Array.from(el.querySelectorAll('[data-testid="feelings-day-detail"] > li'));
  }

  it('lists the day’s check-ins, with Edit only on observations', async () => {
    const { el } = await setup();

    const [own, seen] = items(el);
    expect(items(el)).toHaveLength(2);
    expect(own.textContent).toContain('Ready');
    expect(own.textContent).toContain('Happy');
    expect(own.textContent).toContain('Football');
    expect(own.textContent).toContain('Your child');
    expect(own.textContent).not.toContain('Edit');
    expect(seen.textContent).toContain('Observation');
    expect(seen.textContent).toContain('Edit');
    expect(el.textContent).toContain('can be removed, but not edited');
  });

  it('emits edit, and removes a check-in -- a self-report too', async () => {
    const { fixture, el, removeCheckIn } = await setup();
    const edited: string[] = [];
    const removed: string[] = [];
    fixture.componentInstance.edit.subscribe((c) => edited.push(c.id));
    fixture.componentInstance.removed.subscribe((id) => removed.push(id));

    const [own, seen] = items(el);
    (
      Array.from(seen.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Edit',
      ) as HTMLButtonElement
    ).click();
    (
      Array.from(own.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Remove',
      ) as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(edited).toEqual(['seen']);
    expect(removeCheckIn).toHaveBeenCalledWith('child-1', 'own');
    expect(removed).toEqual(['own']);
  });

  it('shows the remove error', async () => {
    const { fixture, el } = await setup(vi.fn(async () => Promise.reject(new Error('down'))));

    (
      Array.from(items(el)[0].querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Remove',
      ) as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(el.textContent).toContain('Unable to remove the check-in.');
  });
});
