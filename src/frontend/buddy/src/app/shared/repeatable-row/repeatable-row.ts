import { Component, input, output } from '@angular/core';

// Chrome for one row of a repeatable list -- the projected content (e.g. an app-time-select) plus
// a uniformly styled remove action. Extracted from the medicine schedule form's hand-rolled
// dose-times list (the only place this pattern existed) per docs/frontend/analysis/
// visual-specification.md; the parent still owns the array and the "add" action, since only it
// knows the row's shape (a single field today, a time+duration pair for a future Sleep Diary).
@Component({
  selector: 'app-repeatable-row',
  imports: [],
  templateUrl: './repeatable-row.html',
})
export class RepeatableRow {
  readonly canRemove = input(true);
  readonly removeLabel = input.required<string>();

  readonly remove = output<void>();
}
