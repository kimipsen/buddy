import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { FlashCardSet, FlashCardsService } from '../../../core/flash-cards.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { GuardianFlashCards } from './flash-cards';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 4; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function child(id: string, givenName: string): ChildSummary {
  return {
    id,
    name: { givenName, familyName: 'Kid' },
    guardianLinkId: `link-${id}`,
    kind: 'Parent',
    language: 'en',
    timeZoneId: 'UTC',
  } as ChildSummary;
}

const SET: FlashCardSet = {
  id: 's1',
  ownerId: 'g',
  name: 'Plus up to 20',
  kind: 'Math',
  math: { operation: 'Addition', limit: 20 },
  cards: [],
  uniqueCardCount: 231,
  builtInKey: null,
  exampleCard: null,
};

function setup(options: { childParam?: string; sets?: Promise<FlashCardSet[]> } = {}) {
  const getChildFlashCards = vi
    .fn()
    .mockResolvedValue({ childId: 'x', enabledSets: [], games: [] });
  TestBed.configureTestingModule({
    providers: [
      {
        provide: FlashCardsService,
        useValue: {
          listSets: () => options.sets ?? Promise.resolve([SET]),
          getChildFlashCards,
        },
      },
      {
        provide: GuardiansService,
        useValue: {
          listMyChildren: () => Promise.resolve([child('c1', 'Emil'), child('c2', 'Ida')]),
        },
      },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: {
            queryParamMap: convertToParamMap(
              options.childParam ? { child: options.childParam } : {},
            ),
          },
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(GuardianFlashCards);
  return { fixture, el: fixture.nativeElement as HTMLElement, getChildFlashCards };
}

function tab(el: HTMLElement, text: string): HTMLButtonElement {
  return Array.from(el.querySelectorAll<HTMLButtonElement>('button[role="radio"]')).find(
    (b) => b.textContent?.trim() === text,
  )!;
}

describe('GuardianFlashCards', () => {
  it('opens on the library tab', async () => {
    const { fixture, el, getChildFlashCards } = setup();
    await settle(fixture);

    expect(el.querySelector('h2')?.textContent).toContain('Flash cards');
    expect(tab(el, 'Card sets').getAttribute('aria-checked')).toBe('true');
    expect(el.textContent).toContain('Plus up to 20');
    expect(getChildFlashCards).not.toHaveBeenCalled();
  });

  it('shows a loading message, then an error when the sets cannot be loaded', async () => {
    const { fixture, el } = setup({ sets: Promise.reject(new Error('x')) });
    fixture.detectChanges();
    expect(el.textContent).toContain('Loading flash cards…');

    await settle(fixture);
    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'Could not load the flash cards.',
    );
  });

  it('opens the per-child tab on the first child', async () => {
    const { fixture, el, getChildFlashCards } = setup();
    await settle(fixture);

    tab(el, 'Per child').click();
    await settle(fixture);

    expect(getChildFlashCards).toHaveBeenCalledExactlyOnceWith('c1');
    expect(el.textContent).toContain('Flash cards for Emil');
  });

  it('preselects the child and tab from ?child=', async () => {
    const { fixture, el, getChildFlashCards } = setup({ childParam: 'c2' });
    await settle(fixture);

    expect(tab(el, 'Per child').getAttribute('aria-checked')).toBe('true');
    expect(getChildFlashCards).toHaveBeenCalledExactlyOnceWith('c2');
    expect(el.textContent).toContain('Flash cards for Ida');
  });

  it('falls back to the first child for an unknown ?child=', async () => {
    const { fixture, getChildFlashCards } = setup({ childParam: 'nope' });
    await settle(fixture);

    expect(getChildFlashCards).toHaveBeenCalledExactlyOnceWith('c1');
  });

  it('switches child from the selector', async () => {
    const { fixture, el, getChildFlashCards } = setup({ childParam: 'c1' });
    await settle(fixture);

    const select = el.querySelector<HTMLSelectElement>('#flashCardsChildId')!;
    select.value = select.options[1]!.value;
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(getChildFlashCards).toHaveBeenLastCalledWith('c2');
  });
});
