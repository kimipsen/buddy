import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

// Two soft bell notes, rising: a "ding-dong" rather than an alarm, so time's up nudges instead of
// startling.
const NOTES_HZ = [784, 1047];
const NOTE_GAP_S = 0.35;
const NOTE_LENGTH_S = 1.2;
const PEAK_GAIN = 0.3;

// The chime the child's focus view plays when a step's time runs out (GitHub issue #49).
// Synthesised with the Web Audio API, so there's no sound file to ship. Browsers only let a page
// make sound after the user has interacted with it, so call unlock() from a tap; where the browser
// has no Web Audio, or still refuses, nothing plays and the disc alone says time's up.
@Injectable({ providedIn: 'root' })
export class TimeUpChimeService {
  private readonly document = inject(DOCUMENT);
  private context: AudioContext | null = null;

  /** Creates or resumes the audio context; call it from a user gesture (a tap). */
  unlock(): void {
    const context = this.audioContext();
    if (context?.state === 'suspended') {
      context.resume().catch(() => undefined);
    }
  }

  play(): void {
    const context = this.audioContext();
    if (!context || context.state !== 'running') {
      return;
    }

    const startS = context.currentTime;
    NOTES_HZ.forEach((frequency, index) => {
      const atS = startS + index * NOTE_GAP_S;
      const oscillator = context.createOscillator();
      const gain = context.createGain();

      oscillator.type = 'sine';
      oscillator.frequency.value = frequency;
      // A quick attack and a long exponential fade, like a struck bell.
      gain.gain.setValueAtTime(0.0001, atS);
      gain.gain.exponentialRampToValueAtTime(PEAK_GAIN, atS + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, atS + NOTE_LENGTH_S);

      oscillator.connect(gain).connect(context.destination);
      oscillator.start(atS);
      oscillator.stop(atS + NOTE_LENGTH_S);
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
