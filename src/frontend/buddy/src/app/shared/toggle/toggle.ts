import { Component, input, output } from '@angular/core';

// The single visual treatment for every boolean in the app (see docs/frontend/analysis/
// visual-specification.md) -- a native checkbox reads as "select this item", a switch reads as
// "this is currently on", which is what every current use (all-day flag, task/rollup completion,
// calendar visibility) actually means. `size="lg"` is for child-facing screens, where the touch
// target should be closer to 44px than the guardian default.
@Component({
  selector: 'app-toggle',
  imports: [],
  templateUrl: './toggle.html',
})
export class Toggle {
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly size = input<'default' | 'lg'>('default');
  readonly ariaLabel = input('');

  readonly checkedChange = output<boolean>();

  protected toggle(): void {
    if (this.disabled()) {
      return;
    }

    this.checkedChange.emit(!this.checked());
  }
}
