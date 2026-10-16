// The bundled pictograms in public/pictograms/, served at /pictograms/<slug>.svg. An icon value
// is either an emoji or PICTOGRAM_PREFIX + slug (docs/frontend/analysis/pictograms.md). A slug is
// stored in events, so it is never renamed or removed; pictograms.spec.ts keeps this list and the
// folder in step, and the i18n dictionaries hold one name per slug under `pictograms.<slug>`.
export const PICTOGRAM_PREFIX = 'pictogram:';

export const PICTOGRAMS = [
  'bath',
  'brush-teeth',
  'dinner',
  'drink-water',
  'eat-breakfast',
  'get-dressed',
  'go-to-bed',
  'homework',
  'leave-home',
  'lunch',
  'pack-school-bag',
  'play',
  'put-on-shoes',
  'pyjamas-on',
  'read-story',
  'school',
  'screen-time',
  'shower',
  'snack',
  'take-medicine',
  'tidy-room',
  'use-toilet',
  'wake-up',
  'wash-face-hands',
] as const;

export type PictogramSlug = (typeof PICTOGRAMS)[number];

export type ParsedIcon =
  | { readonly kind: 'emoji'; readonly text: string }
  | { readonly kind: 'pictogram'; readonly slug: string; readonly src: string };

export function pictogramIcon(slug: PictogramSlug): string {
  return PICTOGRAM_PREFIX + slug;
}

export function pictogramSrc(slug: string): string {
  return `/pictograms/${slug}.svg`;
}

// The translation key of each pictogram's name, literal so the i18n unused-key check sees them.
const NAME_KEYS: Record<PictogramSlug, string> = {
  bath: 'pictograms.names.bath',
  'brush-teeth': 'pictograms.names.brushTeeth',
  dinner: 'pictograms.names.dinner',
  'drink-water': 'pictograms.names.drinkWater',
  'eat-breakfast': 'pictograms.names.eatBreakfast',
  'get-dressed': 'pictograms.names.getDressed',
  'go-to-bed': 'pictograms.names.goToBed',
  homework: 'pictograms.names.homework',
  'leave-home': 'pictograms.names.leaveHome',
  lunch: 'pictograms.names.lunch',
  'pack-school-bag': 'pictograms.names.packSchoolBag',
  play: 'pictograms.names.play',
  'put-on-shoes': 'pictograms.names.putOnShoes',
  'pyjamas-on': 'pictograms.names.pyjamasOn',
  'read-story': 'pictograms.names.readStory',
  school: 'pictograms.names.school',
  'screen-time': 'pictograms.names.screenTime',
  shower: 'pictograms.names.shower',
  snack: 'pictograms.names.snack',
  'take-medicine': 'pictograms.names.takeMedicine',
  'tidy-room': 'pictograms.names.tidyRoom',
  'use-toilet': 'pictograms.names.useToilet',
  'wake-up': 'pictograms.names.wakeUp',
  'wash-face-hands': 'pictograms.names.washFaceHands',
};

// The translation key of a pictogram's name; a slug this build doesn't have gets a generic name.
export function pictogramNameKey(slug: string): string {
  return isPictogramSlug(slug) ? NAME_KEYS[slug] : 'pictograms.unknown';
}

export function isPictogramSlug(slug: string): slug is PictogramSlug {
  return (PICTOGRAMS as readonly string[]).includes(slug);
}

// An icon where only text fits (a <select> option, a joined label): the emoji itself, and nothing
// for a pictogram, whose name would only repeat the title next to it.
export function iconText(value: string | null | undefined): string {
  return value && !value.startsWith(PICTOGRAM_PREFIX) ? value : '';
}

// null for no icon at all (an empty or missing value). A pictogram whose slug isn't in this build
// still parses as a pictogram: the icon component shows a fallback when its file fails to load.
export function parseIcon(value: string | null | undefined): ParsedIcon | null {
  if (!value) {
    return null;
  }

  if (value.startsWith(PICTOGRAM_PREFIX)) {
    const slug = value.slice(PICTOGRAM_PREFIX.length);
    return { kind: 'pictogram', slug, src: pictogramSrc(slug) };
  }

  return { kind: 'emoji', text: value };
}
