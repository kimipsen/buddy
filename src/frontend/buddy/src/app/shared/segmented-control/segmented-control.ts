import { Component, input, output } from '@angular/core';

export interface SegmentedControlOption<T> {
  readonly value: T;
  readonly label: string;
}

// The visual treatment for a closed set of at most a handful of options known up front (see
// docs/frontend/analysis/visual-specification.md) -- e.g. a pickup assignment kind. A dynamic-length
// list (guardians, siblings, calendars) stays a native `<select>`, which is already styled
// consistently with the rest of the app's inputs.
@Component({
  selector: 'app-segmented-control',
  imports: [],
  templateUrl: './segmented-control.html'
})
export class SegmentedControl<T> {
  readonly options = input.required<SegmentedControlOption<T>[]>();
  readonly selected = input.required<T>();
  readonly ariaLabel = input('');

  readonly selectedChange = output<T>();

  protected isSelected(value: T): boolean {
    return value === this.selected();
  }

  protected select(value: T): void {
    this.selectedChange.emit(value);
  }
}
