import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { TimeUpChimeService } from './time-up-chime.service';

describe('TimeUpChimeService', () => {
  class FakeParam {
    value = 0;
    setValueAtTime = vi.fn();
    exponentialRampToValueAtTime = vi.fn();
  }

  class FakeNode {
    connect = vi.fn((next: FakeNode) => next);
  }

  class FakeOscillator extends FakeNode {
    type = '';
    frequency = new FakeParam();
    start = vi.fn();
    stop = vi.fn();
  }

  class FakeGain extends FakeNode {
    gain = new FakeParam();
  }

  class FakeAudioContext {
    static instances: FakeAudioContext[] = [];
    state: AudioContextState = 'suspended';
    currentTime = 10;
    destination = new FakeNode();
    oscillators: FakeOscillator[] = [];
    gains: FakeGain[] = [];
    resume = vi.fn(async () => {
      this.state = 'running';
    });

    constructor() {
      FakeAudioContext.instances.push(this);
    }

    createOscillator(): FakeOscillator {
      const oscillator = new FakeOscillator();
      this.oscillators.push(oscillator);
      return oscillator;
    }

    createGain(): FakeGain {
      const gain = new FakeGain();
      this.gains.push(gain);
      return gain;
    }
  }

  afterEach(() => {
    FakeAudioContext.instances = [];
  });

  function setup(AudioContextType: unknown = FakeAudioContext): TimeUpChimeService {
    TestBed.configureTestingModule({
      providers: [
        { provide: DOCUMENT, useValue: { defaultView: { AudioContext: AudioContextType } } },
      ],
    });
    return TestBed.inject(TimeUpChimeService);
  }

  it('creates nothing until it is unlocked or played', () => {
    setup();

    expect(FakeAudioContext.instances).toHaveLength(0);
  });

  it('resumes a suspended context on unlock, and reuses it', async () => {
    const chime = setup();

    chime.unlock();
    await Promise.resolve();
    chime.unlock();

    expect(FakeAudioContext.instances).toHaveLength(1);
    expect(FakeAudioContext.instances[0]!.resume).toHaveBeenCalledTimes(1);
  });

  it('plays two rising bell notes once unlocked', async () => {
    const chime = setup();
    chime.unlock();
    await Promise.resolve();

    chime.play();

    const context = FakeAudioContext.instances[0]!;
    expect(context.oscillators.map((oscillator) => oscillator.frequency.value)).toEqual([
      784, 1047,
    ]);
    expect(context.oscillators.map((oscillator) => oscillator.type)).toEqual(['sine', 'sine']);
    expect(context.oscillators.map((oscillator) => oscillator.start.mock.calls[0]![0])).toEqual([
      10, 10.35,
    ]);
    expect(context.oscillators[0]!.stop).toHaveBeenCalledWith(11.2);
    expect(context.oscillators[0]!.connect).toHaveBeenCalledWith(context.gains[0]);
    expect(context.gains[0]!.connect).toHaveBeenCalledWith(context.destination);

    const gain = context.gains[0]!.gain;
    expect(gain.setValueAtTime).toHaveBeenCalledWith(0.0001, 10);
    expect(gain.exponentialRampToValueAtTime.mock.calls).toEqual([
      [0.3, 10.02],
      [0.0001, 11.2],
    ]);
  });

  it('plays three quick rising triangle notes for the xylophone', async () => {
    const chime = setup();
    chime.unlock();
    await Promise.resolve();

    chime.play('Xylophone');

    const context = FakeAudioContext.instances[0]!;
    expect(context.oscillators.map((oscillator) => oscillator.frequency.value)).toEqual([
      523, 659, 784,
    ]);
    expect(context.oscillators.map((oscillator) => oscillator.type)).toEqual([
      'triangle',
      'triangle',
      'triangle',
    ]);
    expect(context.oscillators.map((oscillator) => oscillator.start.mock.calls[0]![0])).toEqual([
      10, 10.18, 10.36,
    ]);
    expect(context.oscillators[0]!.stop).toHaveBeenCalledWith(10.5);
    expect(context.gains[0]!.gain.exponentialRampToValueAtTime.mock.calls).toEqual([
      [0.35, 10.02],
      [0.0001, 10.5],
    ]);
  });

  it('plays one low, long note for the gong', async () => {
    const chime = setup();
    chime.unlock();
    await Promise.resolve();

    chime.play('Gong');

    const context = FakeAudioContext.instances[0]!;
    expect(context.oscillators.map((oscillator) => oscillator.frequency.value)).toEqual([196]);
    expect(context.oscillators[0]!.type).toBe('sine');
    expect(context.oscillators[0]!.stop).toHaveBeenCalledWith(12.5);
    expect(context.gains[0]!.gain.exponentialRampToValueAtTime.mock.calls).toEqual([
      [0.4, 10.02],
      [0.0001, 12.5],
    ]);
  });

  it('stays silent while the browser still blocks audio', () => {
    const chime = setup();

    chime.play();

    expect(FakeAudioContext.instances[0]!.oscillators).toHaveLength(0);
  });

  it('ignores a refused resume', async () => {
    const chime = setup();
    chime.play();
    const context = FakeAudioContext.instances[0]!;
    context.resume.mockRejectedValueOnce(new Error('NotAllowedError'));

    chime.unlock();
    await Promise.resolve();

    expect(context.state).toBe('suspended');
  });

  it('does nothing where the browser has no Web Audio', () => {
    const chime = setup(undefined);

    expect(() => {
      chime.unlock();
      chime.play();
    }).not.toThrow();
  });

  it('stays silent when the context cannot be created', () => {
    const chime = setup(
      class {
        constructor() {
          throw new Error('NotSupportedError');
        }
      },
    );

    expect(() => {
      chime.unlock();
      chime.play();
    }).not.toThrow();
  });
});
