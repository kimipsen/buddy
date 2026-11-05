import type { Schemas } from './api/schemas';
import type { CurrentUser } from './users.service';

// How the child wants their visual timers to look and sound
// (docs/frontend/analysis/child-timer-preferences.md). Pure: the visual timer, the chime and the
// "My timer" page all read the signed-in child's choices through these functions.
export type TimerPreferences = Schemas['TimerPreferences'];
export type TimerShape = Schemas['TimerShape'];
export type TimerSize = Schemas['TimerSize'];
export type TimerColor = Schemas['TimerColor'];
export type ChimeSound = Schemas['ChimeSound'];

// The timer as it was before the child could choose (TimerPreferences.Default on the backend).
export const DEFAULT_TIMER_PREFERENCES: TimerPreferences = {
  shape: 'Disc',
  size: 'Large',
  color: 'Routine',
  showMinutes: true,
  chime: true,
  chimeSound: 'Bell',
  chimeOnHome: false,
};

export const TIMER_SHAPES: readonly TimerShape[] = ['Disc', 'Bar'];
export const TIMER_SIZES: readonly TimerSize[] = ['Small', 'Medium', 'Large'];
export const CHIME_SOUNDS: readonly ChimeSound[] = ['Bell', 'Xylophone', 'Gong'];

// Translation keys for each choice, shared by the child's settings and the guardian's summary.
export const TIMER_SHAPE_LABELS: Readonly<Record<TimerShape, string>> = {
  Disc: 'childTimer.look.shape.Disc',
  Bar: 'childTimer.look.shape.Bar',
};
export const TIMER_SIZE_LABELS: Readonly<Record<TimerSize, string>> = {
  Small: 'childTimer.look.size.Small',
  Medium: 'childTimer.look.size.Medium',
  Large: 'childTimer.look.size.Large',
};
export const TIMER_COLOR_LABELS: Readonly<Record<TimerColor, string>> = {
  Routine: 'childTimer.look.color.Routine',
  Green: 'childTimer.look.color.Green',
  Blue: 'childTimer.look.color.Blue',
  Purple: 'childTimer.look.color.Purple',
  Pink: 'childTimer.look.color.Pink',
  Orange: 'childTimer.look.color.Orange',
  Yellow: 'childTimer.look.color.Yellow',
};
export const CHIME_SOUND_LABELS: Readonly<Record<ChimeSound, string>> = {
  Bell: 'childTimer.sound.sounds.Bell',
  Xylophone: 'childTimer.sound.sounds.Xylophone',
  Gong: 'childTimer.sound.sounds.Gong',
};

// Tailwind 500 shades, as in DEFAULT_COLOR_SWATCHES: each reads against the slate track in light
// and dark mode. 'Routine' has no entry; it means the run's own colour.
export const TIMER_COLORS: Readonly<Record<Exclude<TimerColor, 'Routine'>, string>> = {
  Green: '#10b981',
  Blue: '#0ea5e9',
  Purple: '#a855f7',
  Pink: '#ec4899',
  Orange: '#f97316',
  Yellow: '#f59e0b',
};

// A share of the size the page gives a big timer: the page's size is the upper bound its layout
// allows, so Large is today's size and the others shrink it.
const SIZE_FACTORS: Readonly<Record<TimerSize, number>> = { Small: 0.6, Medium: 0.8, Large: 1 };

export function timerPreferencesOf(user: CurrentUser | null): TimerPreferences {
  return user?.timerPreferences ?? DEFAULT_TIMER_PREFERENCES;
}

// The colour a timer is drawn in: the child's pick, or the colour the page passed.
export function timerColor(preferences: TimerPreferences, pageColor: string): string {
  return preferences.color === 'Routine' ? pageColor : TIMER_COLORS[preferences.color];
}

// A CSS length for a big timer of `size`; Large keeps the page's own expression untouched.
export function scaledTimerSize(pageSize: string, size: TimerSize): string {
  const factor = SIZE_FACTORS[size];
  return factor === 1 ? pageSize : `calc(${pageSize} * ${factor})`;
}

// Where a timer runs out: the focus view and "My timer" chime when the child has the chime on;
// the child home only when they also turned on "chime on home".
export type ChimePlace = 'focus' | 'home' | 'own';

// The sound to play when time runs out at `place`, or null for silence. A guardian's mute
// (allowChime false) wins over anything the child picked.
export function chimeFor(user: CurrentUser | null, place: ChimePlace): ChimeSound | null {
  if (!user || user.allowChime === false) {
    return null;
  }

  const preferences = timerPreferencesOf(user);
  if (!preferences.chime || (place === 'home' && !preferences.chimeOnHome)) {
    return null;
  }

  return preferences.chimeSound;
}
