import { describe, expect, it } from 'vitest';

import {
  DEFAULT_TIMER_PREFERENCES,
  TIMER_COLORS,
  TimerPreferences,
  chimeFor,
  scaledTimerSize,
  timerColor,
  timerPreferencesOf,
} from './timer-preferences';
import type { CurrentUser } from './users.service';

describe('timer preferences', () => {
  const user = (timerPreferences: Partial<TimerPreferences> = {}, allowChime = true): CurrentUser =>
    ({
      allowChime,
      timerPreferences: { ...DEFAULT_TIMER_PREFERENCES, ...timerPreferences },
    }) as CurrentUser;

  it('reads as the defaults before the user has loaded', () => {
    expect(timerPreferencesOf(null)).toEqual(DEFAULT_TIMER_PREFERENCES);
    expect(timerPreferencesOf(user({ shape: 'Bar' })).shape).toBe('Bar');
  });

  it('keeps the page colour for Routine and uses the palette otherwise', () => {
    expect(timerColor(DEFAULT_TIMER_PREFERENCES, '#123456')).toBe('#123456');
    expect(timerColor({ ...DEFAULT_TIMER_PREFERENCES, color: 'Blue' }, '#123456')).toBe(
      TIMER_COLORS.Blue,
    );
  });

  it('draws the six colours in the Tailwind 500 shades that read on the slate track', () => {
    expect(TIMER_COLORS).toEqual({
      Green: '#10b981',
      Blue: '#0ea5e9',
      Purple: '#a855f7',
      Pink: '#ec4899',
      Orange: '#f97316',
      Yellow: '#f59e0b',
    });
  });

  it('scales a big timer below the size the page allows', () => {
    expect(scaledTimerSize('min(70vw, 60vh)', 'Large')).toBe('min(70vw, 60vh)');
    expect(scaledTimerSize('min(70vw, 60vh)', 'Medium')).toBe('calc(min(70vw, 60vh) * 0.8)');
    expect(scaledTimerSize('8rem', 'Small')).toBe('calc(8rem * 0.6)');
  });

  describe('chimeFor', () => {
    it('plays the picked sound on the focus view and my timer', () => {
      expect(chimeFor(user({ chimeSound: 'Xylophone' }), 'focus')).toBe('Xylophone');
      expect(chimeFor(user(), 'own')).toBe('Bell');
    });

    it('stays silent on the home unless the child turned on chime on home', () => {
      expect(chimeFor(user(), 'home')).toBeNull();
      expect(chimeFor(user({ chimeOnHome: true, chimeSound: 'Gong' }), 'home')).toBe('Gong');
    });

    it('stays silent when the child turned the sound off', () => {
      expect(chimeFor(user({ chime: false, chimeOnHome: true }), 'focus')).toBeNull();
      expect(chimeFor(user({ chime: false, chimeOnHome: true }), 'home')).toBeNull();
    });

    it('stays silent when a guardian muted it, whatever the child picked', () => {
      expect(chimeFor(user({ chimeOnHome: true }, false), 'own')).toBeNull();
      expect(chimeFor(user({ chimeOnHome: true }, false), 'home')).toBeNull();
    });

    it('stays silent before the user has loaded', () => {
      expect(chimeFor(null, 'focus')).toBeNull();
    });
  });
});
