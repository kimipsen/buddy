import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ChildWarningFeed } from '../../../core/child-warning-feed.service';
import { TransitionWarning } from '../../../core/transition-warnings';
import {
  REFRESH_CHECK_MS,
  TransitionWarningBanner,
  WARNING_TICK_MS,
} from './transition-warning-banner';

describe('TransitionWarningBanner', () => {
  const NOW = Date.parse('2026-10-13T14:20:00Z');

  function warning(overrides: Partial<TransitionWarning> = {}): TransitionWarning {
    return {
      key: 'item:football:2026-10-13:15',
      kind: 'Event',
      title: 'Football',
      icon: '⚽',
      slot: null,
      startsAtMs: NOW + 10 * 60_000,
      leadMinutes: 15,
      firstLeadMinutes: 15,
      isFinalStage: false,
      ...overrides,
    };
  }

  let fixture: ComponentFixture<TransitionWarningBanner>;

  function setup(warnings: TransitionWarning[], active = true) {
    const feed = {
      active: signal(active),
      warningsAt: vi.fn((nowMs: number) =>
        active ? warnings.filter((w) => w.startsAtMs > nowMs) : [],
      ),
      dismiss: vi.fn(),
      refresh: vi.fn(async () => undefined),
    };
    TestBed.configureTestingModule({
      imports: [TransitionWarningBanner],
      providers: [{ provide: ChildWarningFeed, useValue: feed }],
    });
    fixture = TestBed.createComponent(TransitionWarningBanner);
    fixture.detectChanges();
    return { feed, element: fixture.nativeElement as HTMLElement };
  }

  function card(element: HTMLElement): HTMLElement | null {
    return element.querySelector('[data-testid="transition-warning"]');
  }

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    fixture?.destroy();
    vi.useRealTimers();
  });

  it('renders nothing, not even its live region, and runs no tick while warnings are off', () => {
    const { feed, element } = setup([warning()], false);

    expect(element.innerHTML).not.toContain('role="status"');
    expect(card(element)).toBeNull();
    const calls = feed.warningsAt.mock.calls.length;
    vi.advanceTimersByTime(WARNING_TICK_MS * 4);
    fixture.detectChanges();
    expect(feed.warningsAt.mock.calls.length).toBe(calls);
  });

  it('loads the feed on start, checks it every minute, and reloads when the device wakes', () => {
    const { feed } = setup([], false);
    expect(feed.refresh).toHaveBeenCalledTimes(1);
    expect(feed.refresh).toHaveBeenCalledWith();

    // Asked again every minute even while off, so a guardian switching warnings on is picked up.
    vi.advanceTimersByTime(REFRESH_CHECK_MS);
    expect(feed.refresh).toHaveBeenCalledTimes(2);

    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    document.dispatchEvent(new Event('visibilitychange'));

    expect(feed.refresh).toHaveBeenCalledWith(true);
  });

  it('counts down to the soonest item, announcing only the stage', () => {
    const { element } = setup([warning()]);

    expect(card(element)?.textContent).toContain('10 minutes until Football');
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe(
      '15 minutes until Football',
    );

    vi.advanceTimersByTime(4 * WARNING_TICK_MS);
    fixture.detectChanges();
    expect(card(element)?.textContent).toContain('9 minutes until Football');
    expect(element.querySelector('[role="status"]')?.textContent?.trim()).toBe(
      '15 minutes until Football',
    );
  });

  it('says "1 minute" in the singular and styles the final stage in amber, not red', () => {
    const { element } = setup([
      warning({ startsAtMs: NOW + 50_000, leadMinutes: 1, isFinalStage: true }),
    ]);

    expect(card(element)?.textContent).toContain('1 minute until Football');
    const box = card(element)?.firstElementChild as HTMLElement;
    expect(box.classList).toContain('bg-amber-100');
    expect(box.classList).toContain('dark:bg-amber-950');
    expect(box.outerHTML).not.toContain('red-');
  });

  it('names a pickup by its slot', () => {
    const { element } = setup([
      warning({ kind: 'PickUp', slot: 'PickUp', title: '', key: 'pickup:PickUp:2026-10-13:5' }),
    ]);

    expect(card(element)?.textContent).toContain('10 minutes until pickup');
  });

  it('drains the bar from the first lead time to the start', () => {
    const { element } = setup([warning()]);

    const bar = element.querySelector('[style*="width"]') as HTMLElement;
    expect(bar.style.width).toBe(`${(10 / 15) * 100}%`);
  });

  it('dismisses the current stage on "Got it"', () => {
    const { feed, element } = setup([warning()]);

    const gotIt = [...element.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Got it'),
    );
    gotIt?.click();

    expect(feed.dismiss).toHaveBeenCalledWith('item:football:2026-10-13:15');
  });

  it('opens the other warnings from "+1 more"', () => {
    const { element } = setup([
      warning(),
      warning({ key: 'item:swim:2026-10-13:15', title: 'Swimming', startsAtMs: NOW + 12 * 60_000 }),
    ]);

    const more = [...element.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('+1 more'),
    ) as HTMLButtonElement;
    expect(more.getAttribute('aria-expanded')).toBe('false');
    expect(element.textContent).not.toContain('Swimming');

    more.click();
    fixture.detectChanges();

    expect(more.getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('ul')?.textContent).toContain('12 minutes until Swimming');
  });

  it('hides the banner once the item starts', () => {
    const { element } = setup([warning({ startsAtMs: NOW + 20_000 })]);
    expect(card(element)).not.toBeNull();

    vi.advanceTimersByTime(2 * WARNING_TICK_MS);
    fixture.detectChanges();

    expect(card(element)).toBeNull();
  });
});
