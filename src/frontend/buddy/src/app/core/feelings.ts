import type { FeelingCheckIn, FeelingTag, FeelingZone } from './feelings.service';

// The four zones, each always drawn with a face, its colour and a word, so colour is never the only
// signal (docs/backend/analysis/feelings-check-in.md, Questions 2 and 9). Full translation keys
// rather than concatenation, so --unused stays accurate.
export interface ZoneInfo {
  zone: FeelingZone;
  face: string;
  labelKey: string;
  meaningKey: string;
  colourKey: string;
  faceNameKey: string;
  // Button / chip classes, light and dark.
  buttonClass: string;
  chipClass: string;
  dotClass: string;
  // An observation's mark: the zone's border on a plain background.
  outlineClass: string;
  barClass: string;
}

export const ZONES: readonly ZoneInfo[] = [
  {
    zone: 'Blue',
    face: '😔',
    labelKey: 'feelings.zones.blue.label',
    meaningKey: 'feelings.zones.blue.meaning',
    colourKey: 'feelings.zones.blue.colour',
    faceNameKey: 'feelings.zones.blue.face',
    buttonClass:
      'border-sky-400 bg-sky-100 text-sky-950 dark:border-sky-500 dark:bg-sky-500/20 dark:text-sky-100',
    chipClass:
      'border-sky-300 bg-sky-50 text-sky-900 dark:border-sky-700 dark:bg-sky-500/15 dark:text-sky-100',
    dotClass: 'bg-sky-500 dark:bg-sky-400 border-sky-600 dark:border-sky-300',
    outlineClass: 'border-sky-600 bg-white dark:border-sky-300 dark:bg-slate-900',
    barClass: 'bg-sky-400 dark:bg-sky-500',
  },
  {
    zone: 'Green',
    face: '🙂',
    labelKey: 'feelings.zones.green.label',
    meaningKey: 'feelings.zones.green.meaning',
    colourKey: 'feelings.zones.green.colour',
    faceNameKey: 'feelings.zones.green.face',
    buttonClass:
      'border-emerald-400 bg-emerald-100 text-emerald-950 dark:border-emerald-500 dark:bg-emerald-500/20 dark:text-emerald-100',
    chipClass:
      'border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-100',
    dotClass: 'bg-emerald-500 dark:bg-emerald-400 border-emerald-600 dark:border-emerald-300',
    outlineClass: 'border-emerald-600 bg-white dark:border-emerald-300 dark:bg-slate-900',
    barClass: 'bg-emerald-400 dark:bg-emerald-500',
  },
  {
    zone: 'Yellow',
    face: '😬',
    labelKey: 'feelings.zones.yellow.label',
    meaningKey: 'feelings.zones.yellow.meaning',
    colourKey: 'feelings.zones.yellow.colour',
    faceNameKey: 'feelings.zones.yellow.face',
    buttonClass:
      'border-amber-400 bg-amber-100 text-amber-950 dark:border-amber-500 dark:bg-amber-500/20 dark:text-amber-100',
    chipClass:
      'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-700 dark:bg-amber-500/15 dark:text-amber-100',
    dotClass: 'bg-amber-400 dark:bg-amber-400 border-amber-600 dark:border-amber-300',
    outlineClass: 'border-amber-600 bg-white dark:border-amber-300 dark:bg-slate-900',
    barClass: 'bg-amber-300 dark:bg-amber-500',
  },
  {
    zone: 'Red',
    face: '😡',
    labelKey: 'feelings.zones.red.label',
    meaningKey: 'feelings.zones.red.meaning',
    colourKey: 'feelings.zones.red.colour',
    faceNameKey: 'feelings.zones.red.face',
    buttonClass:
      'border-rose-400 bg-rose-100 text-rose-950 dark:border-rose-500 dark:bg-rose-500/20 dark:text-rose-100',
    chipClass:
      'border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-700 dark:bg-rose-500/15 dark:text-rose-100',
    dotClass: 'bg-rose-500 dark:bg-rose-400 border-rose-700 dark:border-rose-300',
    outlineClass: 'border-rose-700 bg-white dark:border-rose-300 dark:bg-slate-900',
    barClass: 'bg-rose-400 dark:bg-rose-500',
  },
];

const BY_ZONE = new Map(ZONES.map((info) => [info.zone, info]));

export function zoneInfo(zone: FeelingZone): ZoneInfo {
  // Every FeelingZone is in ZONES; the generated contract is the closed list.
  return BY_ZONE.get(zone) as ZoneInfo;
}

// The feeling words that belong to each zone (the backend's FeelingTags.FitsZone), and the
// situations that fit any zone.
export const FEELING_WORDS: Readonly<Record<FeelingZone, readonly FeelingTag[]>> = {
  Blue: ['Tired', 'Sad', 'Bored', 'Unwell', 'Lonely'],
  Green: ['Happy', 'Calm', 'Focused', 'Proud', 'Okay'],
  Yellow: ['Worried', 'Frustrated', 'Silly', 'Excited', 'Restless'],
  Red: ['Angry', 'Scared', 'Overwhelmed'],
};

export const SITUATION_TAGS: readonly FeelingTag[] = [
  'School',
  'Home',
  'Friends',
  'Homework',
  'Screens',
  'Food',
  'Bedtime',
  'Body',
];

export const MAX_TAGS = 3;
export const MAX_NOTE_LENGTH = 280;
export const UNDO_WINDOW_MS = 10 * 60 * 1000;

export const TAG_LABEL_KEYS: Readonly<Record<FeelingTag, string>> = {
  Tired: 'feelings.tags.tired',
  Sad: 'feelings.tags.sad',
  Bored: 'feelings.tags.bored',
  Unwell: 'feelings.tags.unwell',
  Lonely: 'feelings.tags.lonely',
  Happy: 'feelings.tags.happy',
  Calm: 'feelings.tags.calm',
  Focused: 'feelings.tags.focused',
  Proud: 'feelings.tags.proud',
  Okay: 'feelings.tags.okay',
  Worried: 'feelings.tags.worried',
  Frustrated: 'feelings.tags.frustrated',
  Silly: 'feelings.tags.silly',
  Excited: 'feelings.tags.excited',
  Restless: 'feelings.tags.restless',
  Angry: 'feelings.tags.angry',
  Scared: 'feelings.tags.scared',
  Overwhelmed: 'feelings.tags.overwhelmed',
  School: 'feelings.tags.school',
  Home: 'feelings.tags.home',
  Friends: 'feelings.tags.friends',
  Homework: 'feelings.tags.homework',
  Screens: 'feelings.tags.screens',
  Food: 'feelings.tags.food',
  Bedtime: 'feelings.tags.bedtime',
  Body: 'feelings.tags.body',
};

// Keeps the tags that still fit a zone after the zone changed (a situation always fits).
export function tagsFittingZone(tags: readonly FeelingTag[], zone: FeelingZone): FeelingTag[] {
  return tags.filter((tag) => SITUATION_TAGS.includes(tag) || FEELING_WORDS[zone].includes(tag));
}

export interface PromptState {
  // The prompt time now open ("HH:mm"), or null when none is.
  open: string | null;
}

/**
 * Whether a "How are you feeling?" prompt is open (Question 6): from each prompt time until the
 * child checks in at or after it, or the next prompt time comes. A missed prompt just closes; there
 * is no overdue state. `minutesNow` is minutes since the child's midnight (ChildClockService).
 * `checkIns` are the child's own for today -- a guardian's observation never answers a prompt,
 * because the child never receives one.
 */
export function promptState(
  promptTimes: readonly string[],
  checkIns: readonly Pick<FeelingCheckIn, 'time'>[],
  minutesNow: number,
): PromptState {
  const current = [...promptTimes]
    .sort()
    .filter((time) => toMinutes(time) <= minutesNow)
    .at(-1);

  if (current === undefined) {
    return { open: null };
  }

  // Stryker disable next-line MethodExpression: prompt times are "HH:mm", so comparing the full "HH:mm:ss" check-in time orders the same as its first five characters.
  const answered = checkIns.some((checkIn) => checkIn.time.slice(0, 5) >= current);

  return { open: answered ? null : current };
}

// Milliseconds left of the child's undo window for a check-in saved at `savedAtMs`, never negative.
export function undoMsLeft(savedAtMs: number, nowMs: number): number {
  return Math.max(0, savedAtMs + UNDO_WINDOW_MS - nowMs);
}

export function toMinutes(time: string): number {
  return Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));
}
