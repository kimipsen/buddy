import { Component, computed, input } from '@angular/core';

const CENTER = 50;
const HOUR_HAND_LENGTH = 22;
const MINUTE_HAND_LENGTH = 33;
const NUMERAL_RADIUS = 34;
const TICK_OUTER = 46;
const NUMERALS = [12, 3, 6, 9];

// An analogue clock face for the clock flash cards (D12). The hour hand moves with the minutes.
// Colours are theme token classes only; the accessible name comes from the caller, which builds
// it from the hand positions so it never gives away the time words.
@Component({
  selector: 'app-clock-face',
  imports: [],
  templateUrl: './clock-face.html',
})
export class ClockFace {
  readonly hour = input.required<number>();
  readonly minute = input.required<number>();
  readonly ariaLabel = input('');

  protected readonly center = CENTER;
  protected readonly numerals = NUMERALS.map((value) => ({
    value,
    ...ClockFace.point(value * 30, NUMERAL_RADIUS),
  }));
  protected readonly ticks = Array.from({ length: 12 }, (_, i) => ({
    angle: i * 30,
    length: i % 3 === 0 ? 5 : 3,
  }));
  protected readonly tickOuter = TICK_OUTER;

  readonly hourAngle = computed(() => ClockFace.hourAngle(this.hour(), this.minute()));
  readonly minuteAngle = computed(() => ClockFace.minuteAngle(this.minute()));

  protected readonly hourHand = computed(() => ClockFace.point(this.hourAngle(), HOUR_HAND_LENGTH));
  protected readonly minuteHand = computed(() =>
    ClockFace.point(this.minuteAngle(), MINUTE_HAND_LENGTH),
  );

  static hourAngle(hour: number, minute: number): number {
    return (hour % 12) * 30 + minute * 0.5;
  }

  static minuteAngle(minute: number): number {
    return (minute % 60) * 6;
  }

  // Angle in degrees clockwise from 12 o'clock.
  private static point(angle: number, radius: number): { x: number; y: number } {
    const radians = (angle * Math.PI) / 180;
    return { x: CENTER + radius * Math.sin(radians), y: CENTER - radius * Math.cos(radians) };
  }
}
