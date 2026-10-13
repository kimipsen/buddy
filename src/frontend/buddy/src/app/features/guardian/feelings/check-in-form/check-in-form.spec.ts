import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { FeelingCheckIn, FeelingsService } from '../../../../core/feelings.service';
import { FeelingsCheckInForm } from './check-in-form';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(el: HTMLElement, text: string): HTMLButtonElement {
  return Array.from(el.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  ) as HTMLButtonElement;
}

const observation: FeelingCheckIn = {
  id: 'c1',
  zone: 'Red',
  tags: ['Angry'],
  note: 'At pickup',
  date: '2026-10-12',
  time: '15:10',
  loggedBy: 'guardian-1',
  isSelfReport: false,
  recordedAt: '2026-10-12T13:10:00Z',
};

describe('FeelingsCheckInForm', () => {
  async function setup(editing: FeelingCheckIn | null = null) {
    const feelings = {
      logCheckIn: vi.fn(async () => observation),
      editCheckIn: vi.fn(async () => observation),
    } satisfies Partial<FeelingsService>;

    await TestBed.configureTestingModule({
      imports: [FeelingsCheckInForm],
      providers: [{ provide: FeelingsService, useValue: feelings }],
    }).compileComponents();

    const fixture = TestBed.createComponent(FeelingsCheckInForm);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('defaultDate', '2026-10-12');
    fixture.componentRef.setInput('editing', editing);
    await settle(fixture);
    return { fixture, feelings, el: fixture.nativeElement as HTMLElement };
  }

  it('logs an observation now: a zone is all it needs', async () => {
    const { fixture, feelings, el } = await setup();

    expect(button(el, 'Save').disabled).toBe(true);
    (el.querySelector('button[data-zone="Blue"]') as HTMLButtonElement).click();
    await settle(fixture);
    button(el, 'Tired').click();
    await settle(fixture);
    button(el, 'Save').click();
    await settle(fixture);

    expect(feelings.logCheckIn).toHaveBeenCalledWith(
      'child-1',
      { zone: 'Blue', tags: ['Tired'], note: '' },
      null,
      null,
    );
    expect(el.textContent).toContain('Saved.');
  });

  it('logs an earlier moment, defaulting to the selected day, and drops words of another zone', async () => {
    const { fixture, feelings, el } = await setup();

    (el.querySelector('button[data-zone="Yellow"]') as HTMLButtonElement).click();
    await settle(fixture);
    button(el, 'Worried').click();
    button(el, 'School').click();
    await settle(fixture);
    (el.querySelector('button[data-zone="Red"]') as HTMLButtonElement).click();
    await settle(fixture);
    button(el, 'Earlier').click();
    await settle(fixture);

    // Earlier needs a time too.
    expect(button(el, 'Save').disabled).toBe(true);
    fixture.componentInstance['time'].set('15:10');
    await settle(fixture);
    button(el, 'Save').click();
    await settle(fixture);

    expect(feelings.logCheckIn).toHaveBeenCalledWith(
      'child-1',
      { zone: 'Red', tags: ['School'], note: '' },
      '2026-10-12',
      '15:10',
    );
  });

  it('edits an observation in full and emits it', async () => {
    const { fixture, feelings, el } = await setup(observation);
    const saved: FeelingCheckIn[] = [];
    fixture.componentInstance.saved.subscribe((c) => saved.push(c));

    expect(el.textContent).toContain('Edit the observation');
    expect(el.querySelector('button[data-zone="Red"]')?.getAttribute('aria-pressed')).toBe('true');

    (el.querySelector('button[data-zone="Yellow"]') as HTMLButtonElement).click();
    await settle(fixture);
    button(el, 'Save').click();
    await settle(fixture);

    expect(feelings.editCheckIn).toHaveBeenCalledWith('child-1', 'c1', {
      zone: 'Yellow',
      tags: [],
      note: 'At pickup',
      date: '2026-10-12',
      time: '15:10',
    });
    expect(saved).toEqual([observation]);
  });

  it('shows the save error', async () => {
    const { fixture, feelings, el } = await setup();
    feelings.logCheckIn.mockRejectedValueOnce(new Error('400'));

    (el.querySelector('button[data-zone="Green"]') as HTMLButtonElement).click();
    await settle(fixture);
    button(el, 'Save').click();
    await settle(fixture);

    expect(el.textContent).toContain('Unable to save the check-in.');
  });
});
