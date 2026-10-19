import { describe, expect, it } from 'vitest';

import {
  FEELING_WORDS,
  SITUATION_TAGS,
  TAG_LABEL_KEYS,
  UNDO_WINDOW_MS,
  ZONES,
  promptState,
  tagsFittingZone,
  toMinutes,
  undoMsLeft,
  zoneInfo,
} from './feelings';

describe('promptState', () => {
  const prompts = ['07:30', '15:00', '19:30'];

  it('has no prompt open before the first prompt time', () => {
    expect(promptState(prompts, [], toMinutes('07:29'))).toEqual({ open: null });
  });

  it('opens a prompt at its time, whatever order the times come in', () => {
    expect(promptState(['15:00', '07:30'], [], toMinutes('07:30'))).toEqual({ open: '07:30' });
  });

  it('closes a prompt once the child checks in at or after it', () => {
    expect(promptState(prompts, [{ time: '07:31' }], toMinutes('08:00'))).toEqual({ open: null });
    expect(promptState(prompts, [{ time: '07:30:00' }], toMinutes('08:00'))).toEqual({
      open: null,
    });
  });

  it('a check-in before the prompt time does not answer it', () => {
    expect(promptState(prompts, [{ time: '07:00' }], toMinutes('08:00'))).toEqual({
      open: '07:30',
    });
  });

  it('a missed prompt closes when the next one opens, with no overdue state', () => {
    expect(promptState(prompts, [], toMinutes('15:05'))).toEqual({ open: '15:00' });
    expect(promptState(prompts, [{ time: '07:40' }], toMinutes('15:05'))).toEqual({
      open: '15:00',
    });
  });

  it('has nothing open without prompt times', () => {
    expect(promptState([], [], toMinutes('12:00'))).toEqual({ open: null });
  });
});

describe('undoMsLeft', () => {
  const recordedMs = Date.parse('2026-10-13T13:00:00Z');

  it('counts down the ten-minute window and never goes negative', () => {
    expect(undoMsLeft(recordedMs, recordedMs)).toBe(UNDO_WINDOW_MS);
    expect(undoMsLeft(recordedMs, recordedMs + 9 * 60_000)).toBe(60_000);
    expect(undoMsLeft(recordedMs, recordedMs + 10 * 60_000)).toBe(0);
    expect(undoMsLeft(recordedMs, recordedMs + 11 * 60_000)).toBe(0);
  });
});

describe('the zone and tag catalog', () => {
  it('matches the backend catalog: four zones, 18 feeling words, 8 situations', () => {
    expect(ZONES.map((z) => z.zone)).toEqual(['Blue', 'Green', 'Yellow', 'Red']);
    expect(Object.values(FEELING_WORDS).flat()).toHaveLength(18);
    expect(SITUATION_TAGS).toHaveLength(8);
    expect(Object.keys(TAG_LABEL_KEYS)).toHaveLength(26);
  });

  it.each([
    ['Blue', 'sky'],
    ['Green', 'emerald'],
    ['Yellow', 'amber'],
    ['Red', 'rose'],
  ] as const)('draws the %s zone with its own keys and %s colour classes', (zone, colour) => {
    const info = zoneInfo(zone);
    const name = zone.toLowerCase();

    expect(info.face).not.toBe('');
    expect(info.labelKey).toBe(`feelings.zones.${name}.label`);
    expect(info.meaningKey).toBe(`feelings.zones.${name}.meaning`);
    expect(info.colourKey).toBe(`feelings.zones.${name}.colour`);
    expect(info.faceNameKey).toBe(`feelings.zones.${name}.face`);
    for (const classes of [
      info.buttonClass,
      info.chipClass,
      info.dotClass,
      info.outlineClass,
      info.barClass,
    ]) {
      expect(classes).toMatch(new RegExp(`(^| )(bg|border)-${colour}-\\d+`));
      expect(classes).toMatch(new RegExp(`(^| )dark:(bg|border)-`));
    }
  });

  it('gives each zone a distinct face', () => {
    expect(new Set(ZONES.map((z) => z.face)).size).toBe(4);
    expect(zoneInfo('Red').face).toBe('😡');
  });

  it('keeps situations and the new zone’s words when the zone changes', () => {
    expect(tagsFittingZone(['Worried', 'School'], 'Blue')).toEqual(['School']);
    expect(tagsFittingZone(['Worried', 'School'], 'Yellow')).toEqual(['Worried', 'School']);
  });
});
