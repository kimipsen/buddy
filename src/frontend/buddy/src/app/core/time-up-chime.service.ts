import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

import type { ChimeSound } from './timer-preferences';

interface ChimeRecipe {
  readonly notesHz: readonly number[];
  readonly wave: OscillatorType;
  readonly gapS: number;
  readonly lengthS: number;
  readonly peakGain: number;
}

// Every sound nudges rather than startles: soft attacks, long fades, no alarm-like repetition
// (docs/frontend/analysis/child-timer-preferences.md, Question 1).
const RECIPES: Readonly<Record<ChimeSound, ChimeRecipe>> = {
  // Two soft bell notes, rising: a "ding-dong".
  Bell: { notesHz: [784, 1047], wave: 'sine', gapS: 0.35, lengthS: 1.2, peakGain: 0.3 },
  // Three short wooden-sounding notes going up.
  Xylophone: {
    notesHz: [523, 659, 784],
    wave: 'triangle',
    gapS: 0.18,
    lengthS: 0.5,
    peakGain: 0.35,
  },
  // One low, long hum.
  Gong: { notesHz: [196], wave: 'sine', gapS: 0, lengthS: 2.5, peakGain: 0.4 },
};

// The chime the child's timers play when time runs out (GitHub issues #49 and #50).
// Synthesised with the Web Audio API, so there's no sound file to ship. Browsers only let a page
// make sound after the user has interacted with it, so call unlock() from a tap; where the browser
// has no Web Audio, or still refuses, nothing plays and the disc alone says time's up.
@Injectable({ providedIn: 'root' })
export class TimeUpChimeService {
  private readonly document = inject(DOCUMENT);
  private context: AudioContext | null = null;

  /**
   * Creates or resumes the audio context; call it from a user gesture (a tap). Resolves once the
   * context has resumed (or couldn't), for a caller that wants to play straight after.
   */
  unlock(): Promise<void> {
    const context = this.audioContext();
    return context?.state === 'suspended'
      ? context.resume().catch(() => undefined)
      : Promise.resolve();
  }

  play(sound: ChimeSound = 'Bell'): void {
    const context = this.audioContext();
    if (context?.state !== 'running') {
      return;
    }

    const { notesHz, wave, gapS, lengthS, peakGain } = RECIPES[sound];
    const startS = context.currentTime;
    notesHz.forEach((frequency, index) => {
      const atS = startS + index * gapS;
      const oscillator = context.createOscillator();
      const gain = context.createGain();

      oscillator.type = wave;
      oscillator.frequency.value = frequency;
      // A quick attack and a long exponential fade, like a struck bell.
      gain.gain.setValueAtTime(0.0001, atS);
      gain.gain.exponentialRampToValueAtTime(peakGain, atS + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, atS + lengthS);

      oscillator.connect(gain).connect(context.destination);
      oscillator.start(atS);
      oscillator.stop(atS + lengthS);
    });
  }

  private audioContext(): AudioContext | null {
    if (!this.context) {
      const AudioContextType = this.document.defaultView?.AudioContext;
      if (!AudioContextType) {
        return null;
      }

      try {
        this.context = new AudioContextType();
      } catch {
        // Too many contexts, or audio is disabled: this.context stays null, so nothing plays.
      }
    }

    return this.context;
  }
}
