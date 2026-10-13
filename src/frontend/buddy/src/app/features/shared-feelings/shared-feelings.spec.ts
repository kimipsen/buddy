import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { FeelingsService, SharedFeelings as SharedView } from '../../core/feelings.service';
import { SharedFeelings } from './shared-feelings';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

const view: SharedView = {
  childGivenName: 'Emil',
  childFamilyName: 'Hansen',
  from: '2026-09-30',
  to: '2026-10-13',
  expiresAt: '2026-11-12T00:00:00Z',
  checkIns: [
    {
      id: 'c1',
      zone: 'Red',
      tags: ['Overwhelmed', 'School'],
      note: 'Meltdown at pickup',
      date: '2026-10-08',
      time: '15:10',
      loggedBy: 'guardian-1',
      isSelfReport: false,
      recordedAt: '2026-10-08T13:10:00Z',
    },
  ],
};

describe('SharedFeelings', () => {
  async function setup(getShared: FeelingsService['getShared']) {
    await TestBed.configureTestingModule({
      imports: [SharedFeelings],
      providers: [
        { provide: FeelingsService, useValue: { getShared } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ token: 'tok' }) } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(SharedFeelings);
    await settle(fixture);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the child, the 14 days and every check-in with its words and note', async () => {
    const getShared = vi.fn(async () => view);
    const { el } = await setup(getShared);

    expect(getShared).toHaveBeenCalledWith('tok', undefined, undefined);
    expect(el.textContent).toContain('Feelings check-ins for Emil Hansen');
    expect(el.textContent).toContain('This link expires');
    expect(el.querySelectorAll('[data-testid="feelings-trend"] tbody tr')).toHaveLength(14);
    const table = el.querySelector('[data-testid="shared-feelings-table"]')?.textContent ?? '';
    expect(table).toContain('Too much');
    expect(table).toContain('adult');
    expect(table).toContain('Overwhelmed');
    expect(table).toContain('Meltdown at pickup');
    expect(el.textContent).not.toContain('Medicine');
  });

  it('pages back by 14 days', async () => {
    const getShared = vi.fn(async () => view);
    const { fixture, el } = await setup(getShared);

    (
      Array.from(el.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Previous 14 days',
      ) as HTMLButtonElement
    ).click();
    await settle(fixture);

    expect(getShared).toHaveBeenLastCalledWith('tok', '2026-09-16', '2026-09-29');
  });

  it('says a revoked or expired link is gone', async () => {
    const { el } = await setup(
      vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 404 }))),
    );

    expect(el.textContent).toContain('This link is invalid, has been revoked or has expired.');
  });

  it('pages forward only back up to the newest fortnight', async () => {
    const getShared = vi.fn(async (_: string, from?: string, to?: string) =>
      from && to ? { ...view, from, to } : view,
    );
    const { fixture, el } = await setup(getShared);
    const buttonText = (text: string) =>
      Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.trim() === text);

    expect(buttonText('Next 14 days')).toBeUndefined();
    buttonText('Previous 14 days')!.click();
    await settle(fixture);
    expect(buttonText('Next 14 days')).toBeTruthy();

    buttonText('Next 14 days')!.click();
    await settle(fixture);
    expect(getShared).toHaveBeenLastCalledWith('tok', '2026-09-30', '2026-10-13');
    expect(buttonText('Next 14 days')).toBeUndefined();
  });
});
