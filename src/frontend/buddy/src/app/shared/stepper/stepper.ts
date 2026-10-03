import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

// Visual treatment for a small, bounded numeric quantity (subtask duration in minutes, a
// recurrence interval count) -- see docs/frontend/analysis/visual-specification.md. Not used for
// dosage, which is a free-text amount-plus-unit string ("5ml", "1 tablet"), not a count, or for a
// progress goal threshold, whose range is too large for +/- clicks to be the primary input.
@Component({
  selector: 'app-stepper',
  imports: [FormsModule],
  templateUrl: './stepper.html',
})
export class Stepper {
  readonly value = input.required<number>();
  readonly min = input(1);
  readonly max = input(Infinity);
  readonly step = input(1);
  readonly disabled = input(false);
  readonly ariaLabel = input('');
  readonly decrementLabel = input('');
  readonly incrementLabel = input('');

  readonly valueChange = output<number>();

  protected canDecrement(): boolean {
    return !this.disabled() && this.value() - this.step() >= this.min();
  }

  protected canIncrement(): boolean {
    return !this.disabled() && this.value() + this.step() <= this.max();
  }

  protected decrement(): void {
    if (this.canDecrement()) {
      this.valueChange.emit(this.value() - this.step());
    }
  }

  protected increment(): void {
    if (this.canIncrement()) {
      this.valueChange.emit(this.value() + this.step());
    }
  }

  // ngModel on a number input emits null when the field is cleared.
  protected setValue(raw: number | null): void {
    if (raw === null || Number.isNaN(raw)) {
      return;
    }

    const clamped = Math.min(this.max(), Math.max(this.min(), raw));
    this.valueChange.emit(clamped);
  }
}
